using UnityEngine;
using System.Collections.Generic;
using CompanionServer.Handlers;
using System.Drawing.Imaging;
using Graphics = System.Drawing.Graphics;
using ProtoBuf;
using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Linq;
using Oxide.Core;
using System.Collections;
using UnityEngine.Networking;
using Color = System.Drawing.Color;

namespace Oxide.Plugins
{
    [Info("SignTool", "bmgjet", "1.0.2")]
    [Description("SignTool, Insert Images and Skins into map file directly, Then reload them on server startup.")]
    //XML Data LayOut for Image Data
    //<? xml version="1.0"?>
    //<SerializedImageData>
    //        <position>
    //            <x>0</x>
    //            <y>0</y>
    //            <z>0</z>
    //        </position>
    //        <texture>Base64 Image Bytes</texture>
    //</SerializedImageData>

    //XML Data LayOut for Image Data
    //<? xml version="1.0"?>
    //<SerializedSkinData>
    //        <position>
    //            <x>0</x>
    //            <y>0</y>
    //            <z>0</z>
    //        </position>
    //        <skin>uint</skin>
    //</SerializedSkinData>
    public class SignTool : RustPlugin
    {
        //List Of Server Signs Found
        List<Signage> ServerSigns = new List<Signage>();
        //List Of Server Skinnable prefabs Found
        List<BaseEntity> ServerSkinnables = new List<BaseEntity>();
        //IDs of types of signs
        uint[] signids = { 1447270506, 4057957010, 120534793, 58270319, 4290170446, 3188315846, 3215377795, 1960724311, 3159642196, 3725754530, 1957158128, 637495597, 1283107100, 4006597758, 3715545584, 3479792512, 3618197174, 550204242 };
        //IDs of prefabs that are skinnable
        uint[] skinnableids = { 1844023509, 177343599, 3994459244, 4196580066, 3110378351, 2206646561, 2931042549, 159326486, 2245774897, 1560881570, 3647679950, 170207918, 202293038, 1343928398, 43442943, 201071098, 1418678061, 2662124780, 2057881102, 2335812770, 2905007296 };

        //Paintable Signs
        /*
        sign.small.wood.prefab
        sign.post.town.roof.prefab
        sign.post.town.prefab
        sign.post.single.prefab
        sign.post.double.prefab
        sign.pole.banner.large.prefab
        sign.pictureframe.landscape.prefab
        sign.pictureframe.portrait.prefab
        sign.pictureframe.tall.prefab
        sign.pictureframe.xxl.prefab
        sign.pictureframe.xl.prefab
        sign.hanging.banner.large.prefab
        sign.hanging.ornate.prefab
        spinner.wheel.deployed.prefab
        sign.medium.wood.prefab
        sign.large.wood.prefab
        sign.huge.wood.prefab
        sign.hanging.prefab

        //Skinnable Items
        fridge.deployed.prefab
        locker.deployed.prefab
        reactivetarget_deployed.prefab
        rug.deployed.prefab
        rug.bear.deployed.prefab
        box.wooden.large.prefab
        woodbox_deployed.prefab
        furnace.prefab
        sleepingbag_leather_deployed.prefab
        npcvendingmachine.prefab
        wall.frame.garagedoor.prefab
        door.hinged.toptier.prefab
        door.hinged.metal.prefab
        door.hinged.wood.prefab
        door.double.hinged.wood.prefab
        door.double.hinged.toptier.prefab
        door.double.hinged.metal.prefab
        table.deployed.prefab
        barricade.concrete.prefab
        barricade.sandbags.prefab
        waterpurifier.deployed.prefab
        */


        //Admin Permission
        const string PermMap = "SignTool.admin";
        //Sign Data Extracted from MapData
        Dictionary<Vector3, byte[]> SignData = new Dictionary<Vector3, byte[]>();
        //Skin Data Extracted from MapData
        Dictionary<Vector3, uint> SkinData = new Dictionary<Vector3, uint>();
        //Sign Sizes (Thanks to SignArtists code)
        private Dictionary<string, SignSize> _signSizes = new Dictionary<string, SignSize>
        {
            {"spinner.wheel.deployed", new SignSize(512, 512)},
            {"sign.pictureframe.landscape", new SignSize(256, 128)},
            {"sign.pictureframe.tall", new SignSize(128, 512)},
            {"sign.pictureframe.portrait", new SignSize(128, 256)},
            {"sign.pictureframe.xxl", new SignSize(1024, 512)},
            {"sign.pictureframe.xl", new SignSize(512, 512)},
            {"sign.small.wood", new SignSize(128, 64)},
            {"sign.medium.wood", new SignSize(256, 128)},
            {"sign.large.wood", new SignSize(256, 128)},
            {"sign.huge.wood", new SignSize(512, 128)},
            {"sign.hanging.banner.large", new SignSize(64, 256)},
            {"sign.pole.banner.large", new SignSize(64, 256)},
            {"sign.post.single", new SignSize(128, 64)},
            {"sign.post.double", new SignSize(256, 256)},
            {"sign.post.town", new SignSize(256, 128)},
            {"sign.post.town.roof", new SignSize(256, 128)},
            {"sign.hanging", new SignSize(128, 256)},
            {"sign.hanging.ornate", new SignSize(256, 128)},
            {"sign.neon.xl.animated", new SignSize(250, 250)},
            {"sign.neon.xl", new SignSize(250, 250)},
            {"sign.neon.125x215.animated", new SignSize(215, 125)},
            {"sign.neon.125x215", new SignSize(215, 125)},
            {"sign.neon.125x125", new SignSize(125, 125)},
        };

        private readonly Queue<DownloadRequest> downloadQueue = new Queue<DownloadRequest>();

        public interface IBasePaintableEntity
        {
            BaseEntity Entity { get; }
            string PrefabName { get; }
            string ShortPrefabName { get; }
            uint NetId { get; }
            void SendNetworkUpdate();
        }
        public interface IPaintableEntity : IBasePaintableEntity
        {
            void SetImage(uint id, int frameid);
            bool CanUpdate(BasePlayer player);
            uint TextureId();
        }
        public class BasePaintableEntity : IBasePaintableEntity
        {
            public BaseEntity Entity { get; }
            public string PrefabName { get; }
            public string ShortPrefabName { get; }
            public uint NetId { get; }

            protected BasePaintableEntity(BaseEntity entity)
            {
                Entity = entity;
                PrefabName = Entity.PrefabName;
                ShortPrefabName = Entity.ShortPrefabName;
                NetId = Entity.net.ID;
            }

            public void SendNetworkUpdate()
            {
                Entity.SendNetworkUpdate();
            }
        }
        private class PaintableSignage : BasePaintableEntity, IPaintableEntity
        {
            public Signage Sign { get; set; }

            public PaintableSignage(Signage sign) : base(sign)
            {
                Sign = sign;
            }

            public void SetImage(uint id, int frameid)
            {
                Sign.textureIDs[frameid] = id;
            }

            public bool CanUpdate(BasePlayer player)
            {
                return Sign.CanUpdateSign(player);
            }

            public uint TextureId()
            {
                return Sign.textureIDs.First();
            }
        }
        private class PaintableFrame : BasePaintableEntity, IPaintableEntity
        {
            public PhotoFrame Sign { get; set; }

            public PaintableFrame(PhotoFrame sign) : base(sign)
            {
                Sign = sign;
            }

            public void SetImage(uint id, int frameid)
            {
                Sign._overlayTextureCrc = id;
            }

            public bool CanUpdate(BasePlayer player)
            {
                return Sign.CanUpdateSign(player);
            }

            public uint TextureId()
            {
                return Sign._overlayTextureCrc;
            }
        }

        private class SignSize
        {
            public int Width;
            public int Height;
            public int ImageWidth;
            public int ImageHeight;
            public SignSize(int width, int height)
            {
                Width = width;
                Height = height;
                ImageWidth = width;
                ImageHeight = height;
            }
        }
        private void Init()
        {
            //Setup Permission
            permission.RegisterPermission(PermMap, this);
        }
        bool isSign(PrefabData sign)
        {
            //Checks prefab has a valid sign id
            return (signids.Contains(sign.id));
        }

        bool isSkinnable(PrefabData skinid)
        {
            //Checks prefab has a valid skinnable id
            return (skinnableids.Contains(skinid.id));
        }
        public string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
        public string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }
        void OnServerInitialized()
        {
            //Delay to give time for everything to fully spawn in.
            timer.Once(10f, () =>
            {

                //Extract Map Data
                for (int i = World.Serialization.world.maps.Count - 1; i >= 0; i--)
                {
                    MapData mapdata = World.Serialization.world.maps[i];
                    if (mapdata.name == Base64Encode("SerializedImageData"))
                    {
                        //Process ImageData
                        XMLDecode(System.Text.Encoding.ASCII.GetString(mapdata.data));
                        Puts("Processed SerializedImageData " + SignData.Count.ToString() + " Images Found");
                    }
                    else if (mapdata.name == Base64Encode("SerializedSkinData"))
                    {
                        //Process SkinData
                        XMLDecodeSkin(System.Text.Encoding.ASCII.GetString(mapdata.data));
                        Puts("Processed SerializedSkinData " + SkinData.Count.ToString() + " Skins Found");
                    }
                }
                //Find All Server Signs
                for (int i = World.Serialization.world.prefabs.Count - 1; i >= 0; i--)
                {
                    PrefabData prefabdata = World.Serialization.world.prefabs[i];
                    if (isSign(prefabdata))
                    {
                        foreach (Signage s in FindSign(prefabdata.position, 0.2f))
                        {
                            ServerSigns.Add(s);
                        }
                    }
                    if (isSkinnable(prefabdata))
                    {
                        foreach (BaseEntity s in FindSkin(prefabdata.position, 0.55f))
                        {
                            ServerSkinnables.Add(s);
                        }
                    }
                }
                Puts("Found " + ServerSigns.Count.ToString() + " Server Signs");
                Puts("Found " + ServerSkinnables.Count.ToString() + " Server Skinnable Items");
                //Check if there is sign data
                if (ServerSigns.Count != 0)
                {
                    //Apply Sign Data to Found Signs
                    foreach (Signage ss in ServerSigns)
                    {
                        Puts("Found Sign @ " + ss.transform.position);
                        foreach (KeyValuePair<Vector3, byte[]> sd in SignData)
                        {
                            if (Vector3.Distance(sd.Key, ss.transform.position) < 0.2)
                            {
                                Puts("Applying Image");
                                ApplySignage(ss, sd.Value, 0);
                                ss.SetFlag(BaseEntity.Flags.Locked, true);
                                ss.SendNetworkUpdate();
                            }
                        }
                    }
                }
                if (ServerSkinnables.Count != 0)
                {
                    //Apply Skin Data to Found Skinnables
                    foreach (BaseEntity ss in ServerSkinnables)
                    {
                        Puts("Found skinnable @ " + ss.transform.position);
                        foreach (KeyValuePair<Vector3, uint> sd in SkinData)
                        {
                            if (Vector3.Distance(sd.Key, ss.transform.position) < 0.2)
                            {
                                Puts("Applying skin");
                                ApplySkin(ss, sd.Value);
                            }
                        }
                    }
                }
            });
        }
        List<Signage> FindSign(Vector3 pos, float radius)
        {
            //Casts a sphere at given position and find all signs there
            var hits = Physics.SphereCastAll(pos, radius, Vector3.up);
            var x = new List<Signage>();
            foreach (var hit in hits)
            {
                var entity = hit.GetEntity()?.GetComponent<Signage>();
                if (entity && !x.Contains(entity))
                    x.Add(entity);
            }
            return x;
        }
        List<BaseEntity> FindSkin(Vector3 pos, float radius)
        {
            //Casts a sphere at given position and find all signs there
            var hits = Physics.SphereCastAll(pos, radius, Vector3.up);
            var x = new List<BaseEntity>();
            foreach (var hit in hits)
            {
                var entity = hit.GetEntity()?.GetComponent<BaseEntity>();
                if (entity && !x.Contains(entity))
                    x.Add(entity);
            }
            return x;
        }
        //(Thanks to SignArtists code)
        void ApplySignage(Signage sign, byte[] imageBytes, int index)
        {
            //Apply Image to sign
            if (!_signSizes.ContainsKey(sign.ShortPrefabName))
                return;

            var size = Math.Max(sign.paintableSources.Length, 1);
            if (sign.textureIDs == null || sign.textureIDs.Length != size)
            {
                Array.Resize(ref sign.textureIDs, size);
            }
            var resizedImage = ImageResize(imageBytes, _signSizes[sign.ShortPrefabName].Width,
                _signSizes[sign.ShortPrefabName].Height);

            sign.textureIDs[index] = FileStorage.server.Store(resizedImage, FileStorage.Type.png, sign.net.ID);
        }

        void ApplySkin(BaseEntity item, uint SkinID)
        {
            //Apply Skin to item
            item.skinID = SkinID;
            item.SendNetworkUpdate();

        }
        //(Thanks to SignArtists code)
        byte[] ImageResize(byte[] imageBytes, int width, int height)
        {
            //Resize image to sign size.
            Bitmap resizedImage = new Bitmap(width, height),
                sourceImage = new Bitmap(new MemoryStream(imageBytes));

            Graphics.FromImage(resizedImage).DrawImage(sourceImage, new Rectangle(0, 0, width, height),
                new Rectangle(0, 0, sourceImage.Width, sourceImage.Height), GraphicsUnit.Pixel);

            var ms = new MemoryStream();
            resizedImage.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }
        //Decodes XML data from MapData
        void XMLDecode(string SerialData)
        {
            string[] DataParse = SerialData.Split(new string[] { "<position>" }, StringSplitOptions.None);
            foreach (string xmldata in DataParse)
            {
                if (xmldata.Contains("xml version")) continue;
                string x = xmldata.Split(new string[] { "</x><y>" }, StringSplitOptions.None)[0].Replace("<x>", "");
                string y = xmldata.Split(new string[] { "</y><z>" }, StringSplitOptions.None)[0].Replace("<x>" + x + "</x><y>", "");
                string z = xmldata.Split(new string[] { "</z></position>" }, StringSplitOptions.None)[0].Replace("<x>" + x + "</x><y>" + y + "</y><z>", "");
                string texture = xmldata.Split(new string[] { "<texture>" }, StringSplitOptions.None)[1].Replace("</texture>", "").Replace("</SerializedImageData>", "");
                byte[] ImageData = Convert.FromBase64String(texture);
                Vector3 pos = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
                SignData.Add(pos, ImageData);
            }
        }
        void XMLDecodeSkin(string SerialData)
        {
            string[] DataParse = SerialData.Split(new string[] { "<position>" }, StringSplitOptions.None);
            foreach (string xmldata in DataParse)
            {
                if (xmldata.Contains("xml version")) continue;
                string x = xmldata.Split(new string[] { "</x><y>" }, StringSplitOptions.None)[0].Replace("<x>", "");
                string y = xmldata.Split(new string[] { "</y><z>" }, StringSplitOptions.None)[0].Replace("<x>" + x + "</x><y>", "");
                string z = xmldata.Split(new string[] { "</z></position>" }, StringSplitOptions.None)[0].Replace("<x>" + x + "</x><y>" + y + "</y><z>", "");
                uint skinid = uint.Parse(xmldata.Split(new string[] { "<skin>" }, StringSplitOptions.None)[1].Replace("</skin>", "").Replace("</SerializedSkinData>", ""));
                Vector3 pos = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
                SkinData.Add(pos, skinid);
            }
        }
        //Create XML Data
        string XMLEncode()
        {
            string XMLData = @"<? xml version=""1.0""?><SerializedImageData>";
            string SerialData = "";
            foreach (Signage _sign in ServerSigns)
            {
                for (int num = 0; num < _sign.textureIDs.Length; num++)
                {
                    var textureId = _sign.textureIDs[num];
                    if (textureId == 0)
                        continue;

                    var imageByte = FileStorage.server.Get(textureId, FileStorage.Type.png, _sign.net.ID);
                    if (imageByte != null)
                    {
                        SerialData += ("<position>" +
                               "<x>" + _sign.transform.position.x.ToString("0.0") + "</x>" +
                               "<y>" + _sign.transform.position.y.ToString("0.0") + "</y>" +
                               "<z>" + _sign.transform.position.z.ToString("0.0") + "</z>" +
                               "</position>" +
                               "<texture>" +
                               Convert.ToBase64String(imageByte) +
                               "</texture>");
                        continue;
                    }
                }
            }
            XMLData = XMLData + SerialData + "</SerializedImageData>";
            return XMLData;
        }
        string XMLEncodeSkin()
        {
            string XMLData = @"<? xml version=""1.0""?><SerializedSkinData>";
            string SerialData = "";
            foreach (BaseEntity _skin in ServerSkinnables)
            {

                if (_skin.skinID != 0)
                {
                    SerialData += ("<position>" +
                               "<x>" + _skin.transform.position.x.ToString("0.0") + "</x>" +
                               "<y>" + _skin.transform.position.y.ToString("0.0") + "</y>" +
                               "<z>" + _skin.transform.position.z.ToString("0.0") + "</z>" +
                                   "</position>" +
                                   "<skin>" +
                                   _skin.skinID.ToString() +
                                   "</skin>");
                }
            }

            XMLData = XMLData + SerialData + "</SerializedSkinData>";
            return XMLData;
        }

        private bool IsLookingAtSign(BasePlayer player, out IPaintableEntity sign)
        {
            RaycastHit hit;
            sign = null;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, 5f))
            {
                BaseEntity entity = hit.GetEntity();
                if (entity is Signage)
                {
                    sign = new PaintableSignage(entity as Signage);
                }
                else if (entity is PhotoFrame)
                {
                    sign = new PaintableFrame(entity as PhotoFrame);
                }
            }

            // Return true or false depending on if we found a sign.
            return sign != null;
        }

        //Skins Items that are being looked at.
        [ChatCommand("sskin")]
        private void SkinCommand(BasePlayer basePlayer, string command, string[] args)
        {
            if (basePlayer == null)
            {
                return;
            }
            if (!permission.UserHasPermission(basePlayer.UserIDString, PermMap))
            {
                //Dont have Permission to use so exit.
                return;
            }
            ulong skin;
            if (args.Length != 1 || !ulong.TryParse(args[0], out skin))
            {
                basePlayer.ChatMessage("Invalid Skin ID");
                return;
            }

            RaycastHit hit;
            if (!Physics.Raycast(basePlayer.eyes.HeadRay(), out hit))
            {
                basePlayer.ChatMessage("No Skinnable Entitys Found. Try Looking at the Hinge area if its a door.");
                return;
            }

            var entity = hit.GetEntity();
            if (entity == null)
            {
                basePlayer.ChatMessage("No Skinnable Entitys Found. Try Looking at the Hinge area if its a door.");
                return;
            }

            entity.skinID = skin;
            entity.SendNetworkUpdateImmediate();
            basePlayer.ChatMessage("Applying Skin");
        }
        private class DownloadRequest
        {
            public BasePlayer Sender { get; }
            public IPaintableEntity Sign { get; }
            public string Url { get; set; }
            public bool Raw { get; }
            public bool Hor { get; }

            public DownloadRequest(string url, BasePlayer player, IPaintableEntity sign, bool raw, bool hor)
            {
                Url = url;
                Sender = player;
                Sign = sign;
                Raw = raw;
                Hor = hor;
            }
        }

        [ChatCommand("ssign")]
        void signcommand(BasePlayer basePlayer, string command, string[] args)
        {
            if (!permission.UserHasPermission(basePlayer.UserIDString, PermMap))
            {
                //Dont have Permission to use so exit.
                return;
            }
            if (args.Length < 1)
            {
                basePlayer.ChatMessage("Invalid Args");
                return;
            }
            IPaintableEntity sign;
            if (!IsLookingAtSign(basePlayer, out sign))
            {
                basePlayer.ChatMessage("No Signs Found");
                return;
            }
            // This sign pastes in reverse, so we'll check and set a var to flip it
            bool hor = sign.ShortPrefabName == "sign.hanging";
            downloadQueue.Enqueue(new DownloadRequest(args[0], basePlayer, sign, false, hor));

            // Attempt to start the next download.
            StartNextDownload();
            Interface.Oxide.CallHook("OnImagePost", basePlayer, args[0]);
        }

        private void StartNextDownload(bool reduceCount = false)
        {
            ServerMgr.Instance.StartCoroutine(DownloadImage(downloadQueue.Dequeue()));
        }
        private SignSize GetImageSizeFor(IPaintableEntity signage)
        {
            if (_signSizes.ContainsKey(signage.ShortPrefabName))
            {
                return _signSizes[signage.ShortPrefabName];
            }

            return null;
        }
        private IEnumerator DownloadImage(DownloadRequest request)
        {
            byte[] imageBytes;
            if (request.Url.StartsWith("data:image"))
            {
                imageBytes = LoadImage(request.Url);
            }
            else
            {
                UnityWebRequest www = UnityWebRequest.Get(request.Url);

                yield return www.SendWebRequest();

                // Verify that there is a valid reference to the plugin from this class.

                // Verify that the webrequest was succesful.
                if (www.isNetworkError || www.isHttpError)
                {
                    // The webrequest wasn't succesful, show a message to the player and attempt to start the next download.
                    request.Sender.ChatMessage("Download Error");
                    www.Dispose();
                    StartNextDownload(true);
                    yield break;
                }

                // Get the bytes array for the image from the webrequest and lookup the target image size for the targeted sign.
                if (request.Raw)
                {
                    imageBytes = www.downloadHandler.data;
                }
                else
                {
                    imageBytes = GetImageBytes(www);
                }
                www.Dispose();
            }
            SignSize size = GetImageSizeFor(request.Sign);

            // Verify that we have image size data for the targeted sign.

            RotateFlipType rotation = RotateFlipType.RotateNoneFlipNone;
            if (request.Hor)
            {
                rotation = RotateFlipType.RotateNoneFlipX;
            }

            object rotateObj = Interface.Call("GetImageRotation", request.Sign.Entity);
            if (rotateObj is RotateFlipType)
            {
                rotation = (RotateFlipType)rotateObj;
            }

            // Get the bytes array for the resized image for the targeted sign.
            byte[] resizedImageBytes = ResizeImage(imageBytes, size.Width, size.Height, size.ImageWidth, size.ImageHeight, false && !request.Raw, rotation);

            // Check if the sign already has a texture assigned to it.
            if (request.Sign.TextureId() > 0)
            {
                // A texture was already assigned, remove this file to make room for the new one.
                FileStorage.server.Remove(request.Sign.TextureId(), FileStorage.Type.png, request.Sign.NetId);
            }

            // Create the image on the filestorage and send out a network update for the sign.
            request.Sign.SetImage(FileStorage.server.Store(resizedImageBytes, FileStorage.Type.png, request.Sign.NetId), 0);
            request.Sign.SendNetworkUpdate();

            // Notify the player that the image was loaded.
            request.Sender.ChatMessage("Sign Updated");

            // Call the Oxide hook 'OnSignUpdated' to notify other plugins of the update event.
            Interface.Oxide.CallHook("OnSignUpdated", request.Sign, request.Sender);

            // Attempt to start the next download.
            StartNextDownload(true);
        }


        private byte[] GetImageBytes(UnityWebRequest www)
        {
            Texture2D texture = new Texture2D(2, 2);
            texture.LoadImage(www.downloadHandler.data);

            byte[] image;

            if (texture.format == TextureFormat.ARGB32)
            {
                image = texture.EncodeToPNG();
            }
            else
            {
                image = texture.EncodeToJPG(90);
            }
            return image;
        }
        private byte[] LoadImage(string data)
        {
            data = data.Replace("data:image/gif;base64,", "");
            data = data.Replace("data:image/jpeg;base64,", "");
            data = data.Replace("data:image/png;base64,", "");
            return Convert.FromBase64String(data);
        }

        public static byte[] ResizeImage(byte[] bytes, int width, int height, int targetWidth, int targetHeight, bool enforceJpeg, RotateFlipType rotation = RotateFlipType.RotateNoneFlipNone)
        {
            byte[] resizedImageBytes;

            using (MemoryStream originalBytesStream = new MemoryStream(), resizedBytesStream = new MemoryStream())
            {
                // Write the downloaded image bytes array to the memorystream and create a new Bitmap from it.
                originalBytesStream.Write(bytes, 0, bytes.Length);
                Bitmap image = new Bitmap(originalBytesStream);

                if (rotation != RotateFlipType.RotateNoneFlipNone)
                {
                    image.RotateFlip(rotation);
                }

                // Check if the width and height match, if they don't we will have to resize this image.
                if (image.Width != targetWidth || image.Height != targetHeight)
                {
                    // Create a new Bitmap with the target size.
                    Bitmap resizedImage = new Bitmap(width, height);

                    // Draw the original image onto the new image and resize it accordingly.
                    using (System.Drawing.Graphics graphics = System.Drawing.Graphics.FromImage(resizedImage))
                    {
                        graphics.DrawImage(image, new Rectangle(0, 0, targetWidth, targetHeight));
                    }

                    TimestampImage(resizedImage);

                    // Save the bitmap to a MemoryStream as either Jpeg or Png.
                    if (enforceJpeg)
                    {
                        resizedImage.Save(resizedBytesStream, ImageFormat.Jpeg);
                    }
                    else
                    {
                        resizedImage.Save(resizedBytesStream, ImageFormat.Png);
                    }

                    // Grab the bytes array from the new image's MemoryStream and dispose of the resized image Bitmap.
                    resizedImageBytes = resizedBytesStream.ToArray();
                    resizedImage.Dispose();
                }
                else
                {
                    TimestampImage(image);
                    // The image has the correct size so we can just return the original bytes without doing any resizing.
                    resizedImageBytes = bytes;
                }

                // Dispose of the original image Bitmap.
                image.Dispose();
            }

            // Return the bytes array.
            return resizedImageBytes;
        }
        private static void TimestampImage(Bitmap image)
        {
            //Rust images are crc and if we have the same image it is deleted from the file storage
            //Here we changed the last few pixels of the image with colors based off the current milliseconds since wipe
            //This will generate a unique image every time and allow us to use the same image multiple times
            Color pixel = Color.FromArgb(UnityEngine.Random.Range(0, 256), UnityEngine.Random.Range(0, 256), UnityEngine.Random.Range(0, 256), UnityEngine.Random.Range(0, 256));
            image.SetPixel(image.Width - 1, image.Height - 1, pixel);
        }

        //Inserts Data from signs placed in the map into MapData
        [ChatCommand("MapSave")]
        void MapSave(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermMap))
            {
                //Dont have Permission to use so exit.
                return;
            }
            //Create XML Data
            string XMLData = XMLEncode();
            string XMLDataSkin = XMLEncodeSkin();

            //Check if mapdata already has image data
            MapData sd = World.Serialization.GetMap(Base64Encode("SerializedImageData"));
            MapData ssd = World.Serialization.GetMap(Base64Encode("SerializedSkinData"));
            if (sd == null)
            {
                player.ChatMessage("Creating Sign Data In Map");
                World.Serialization.AddMap(Base64Encode("SerializedImageData"), Encoding.ASCII.GetBytes(XMLData));
            }
            else
            {
                player.ChatMessage("Updating Sign Data In Map");
                sd.data = Encoding.ASCII.GetBytes(XMLData);
            }
            if (ssd == null)
            {
                player.ChatMessage("Creating Skin Data In Map");
                World.Serialization.AddMap(Base64Encode("SerializedSkinData"), Encoding.ASCII.GetBytes(XMLDataSkin));
            }
            else
            {
                player.ChatMessage("Updating Skin Data In Map");
                ssd.data = Encoding.ASCII.GetBytes(XMLDataSkin);
            }
            string mapname = World.MapFileName.ToString().Replace(".map", ".embeded.map");
            World.Serialization.Save(mapname);
            player.ChatMessage("Saved edited map in root dir as " + mapname);
        }
    }
}