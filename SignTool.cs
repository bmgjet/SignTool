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
namespace Oxide.Plugins
{
    [Info("SignTool", "bmgjet", "1.0.0")]
    [Description("SignTool, Insert Images into map file directly, Then reload them on server startup.")]
    //XML Data LayOut
    //<? xml version="1.0"?>
    //<SerializedImageData>
    //        <position>
    //            <x>0</x>
    //            <y>0</y>
    //            <z>0</z>
    //        </position>
    //        <texture>Base64 Image Bytes</texture>
    //</SerializedImageData>
    public class SignTool : RustPlugin
    {
        //List Of Server Signs Found
        List<Signage> ServerSigns = new List<Signage>();
        //IDs of types of signs
        uint[] signids = { 708840119, 2628005754, 3591916872, 3919686896, 1447270506, 4057957010, 120534793, 58270319, 4290170446, 3188315846, 3215377795, 1960724311, 3159642196, 3725754530, 1957158128 };
        //Admin Permission
        const string PermMap = "SignTool.admin";
        //Sign Data Extracted from MapData
        Dictionary<Vector3, byte[]> SignData = new Dictionary<Vector3, byte[]>();
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
        private class SignSize
        {
            public int Width;
            public int Height;
            public SignSize(int width, int height)
            {
                Width = width;
                Height = height;
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
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }
        void OnServerInitialized()
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
            }
            //Find All Server Signs
            for (int i = World.Serialization.world.prefabs.Count - 1; i >= 0; i--)
            {
                PrefabData prefabdata = World.Serialization.world.prefabs[i];
                if (isSign(prefabdata))
                {
                    foreach (Signage s in FindSign(prefabdata.position, 0.1f))
                    {
                        ServerSigns.Add(s);
                    }
                }
            }
            Puts("Found " + ServerSigns.Count.ToString() + " Server Signs");
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
            //Check if mapdata already has image data
            MapData sd = World.Serialization.GetMap(Base64Encode("SerializedImageData"));
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
            World.Serialization.Save("ServerSaved.map");
            player.ChatMessage("Saved edited map in root dir as ServerSaved.map");
        }
    }
}