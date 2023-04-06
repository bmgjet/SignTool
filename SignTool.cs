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
using Oxide.Core.Plugins;
using System.Reflection;


namespace Oxide.Plugins
{
    [Info("SignTool", "bmgjet", "1.0.7")]
    [Description("SignTool, Insert Images, Skins,Scale into map file directly, Then reload them on server startup.")]
    //XML Data LayOut for Image Data
    //<? xml version="1.0"?>
    //<SerializedImageData>
    //        <position>
    //            <x>0</x>
    //            <y>0</y>
    //            <z>0</z>
    //        </position>
    //        <texture>Base64 Image Bytes<frame>X<frame>.....</texture>
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

    //XML Data LayOut for Embedded Plugins
    //<? xml version="1.0"?>
    //<SerializedPluginData>
    //        <name>String<name>
    //        <data>Base64</data>
    //</SerializedPluginData>
    public class SignTool : RustPlugin
    {
        //Debug Output
        bool showDebug = true;
        //Overwrite Exsisting Plugins
        bool OverWrite = false;
        //Plugins
        public Dictionary<string, string> PluginsData = new Dictionary<string, string>();
        //Temp List Of Things Scales Applied Too.
        List<BaseEntity> ScaledEntitys = new List<BaseEntity>();
        //Protected Entitys against distruction
        List<BaseEntity> Protected = new List<BaseEntity>();
        //List Of Server Signs Found
        Dictionary<Signage, Vector3> ServerSigns = new Dictionary<Signage, Vector3>();
        //List Of Server Skinnable prefabs Found
        Dictionary<BaseEntity, Vector3> ServerSkinnables = new Dictionary<BaseEntity, Vector3>();
        //List of server RE Scaleable Prefabs
        Dictionary<BaseEntity, Vector3> ServerScalable = new Dictionary<BaseEntity, Vector3>();
        //IDs of types of signs
        uint[] signids = { 1447270506, 4057957010, 120534793, 58270319, 4290170446, 3188315846, 3215377795, 1960724311, 3159642196, 3725754530, 1957158128, 637495597, 1283107100, 4006597758, 3715545584, 3479792512, 3618197174, 550204242 };
        //IDs of prefabs that are skinnable
        uint[] skinnableids = { 1844023509, 177343599, 3994459244, 4196580066, 3110378351, 2206646561, 2931042549, 159326486, 2245774897, 1560881570, 3647679950, 170207918, 202293038, 1343928398, 43442943, 201071098, 1418678061, 2662124780, 2057881102, 2335812770, 2905007296, 34236153, 3884356627 };
        //Deployables in RE to check scale of
        uint[] ScaleableRE = { 34236153, 184980835, 4094102585, 4111973013, 244503553 };
        //Neon sign Ids
        uint[] Neons = { 708840119, 3591916872, 3919686896, 2628005754, 3168507223 };
        /*
         
        //Paintable Signs
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

        //Neon Patched
        sign.neon.xl.prefab
        sign.neon.xl.animated.prefab
        sign.neon.125x215.animated.prefab
        sign.neon.125x215.prefab
        sign.neon.125x125.prefab

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

        //ScaleableRE IO Entitys
        sliding_blast_door.prefab
        door.hinged.security.blue.prefab
        door.hinged.security.green.prefab
        door.hinged.security.red.prefab
        boombox.deployed.prefab
        */

        //Admin Permission
        const string PermMap = "SignTool.admin";
        //Sign Data Extracted from MapData
        Dictionary<Vector3, List<byte[]>> SignData = new Dictionary<Vector3, List<byte[]>>();
        //Skin Data Extracted from MapData
        Dictionary<Vector3, uint> SkinData = new Dictionary<Vector3, uint>();
        //Sign Sizes (Thanks to SignArtists code)
        private Dictionary<string, SignSize> _signSizes = new Dictionary<string, SignSize>
        {
            {"spinner.wheel.deployed", new SignSize(64, 64)},
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
        //A blank Alpha Pixel (Stored as base64 since takes less resources then creating one with png class.
        public String Blanked = "iVBORw0KGgoAAAANSUhEUgAAANcAAAB9CAYAAAAx+vY9AAAAAXNSR0IArs4c6QAAAARnQU1BAACxjwv8YQUAAAAJcEhZcwAADsMAAA7DAcdvqGQAAAB/SURBVHhe7cGBAAAAAMOg+VNf4QBVAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA8aqR4AAFsKyZjAAAAAElFTkSuQmCC";

        public static void CopyTo(Stream src, Stream dest)
        {
            byte[] bytes = new byte[4096];

            int cnt;

            while ((cnt = src.Read(bytes, 0, bytes.Length)) != 0)
            {
                dest.Write(bytes, 0, cnt);
            }
        }

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

        private void OnWorldPrefabSpawned(GameObject gameObject, string str)
        {
            //Fix Neons Loading by removing them and storing while server loads.
            BaseEntity component = gameObject.GetComponent<BaseEntity>();
            if (component != null)
            {
                if ((component.prefabID == 708840119 || component.prefabID == 3591916872 || component.prefabID == 3919686896 || component.prefabID == 2628005754 || component.prefabID == 3168507223 || component.prefabID == 1599225199 || component.prefabID == 672916883 || component.prefabID == 2806489601) && component.OwnerID == 0)
                {
                    //Kill all the Neons that server Created.
                    component.Kill();
                }
            }
        }

        //Protect Signs against Editing
        object CanUpdateSign(BaseEntity sign)
        {
            if (Protected.Contains(sign))
            {
                Puts("Block Edit");
                return false;
            }
            return null;
        }

        bool isSign(PrefabData sign)
        {
            //Checks prefab has a valid sign id
            return (signids.Contains(sign.id) || Neons.Contains(sign.id));
        }

        bool isSkinnable(PrefabData skinid)
        {
            //Checks prefab has a valid skinnable id
            return (skinnableids.Contains(skinid.id));
        }

        bool isScaleable(PrefabData scale)
        {
            //Checks prefab has a valid skinnable id
            return (ScaleableRE.Contains(scale.id));
        }

        private void LoadPlugins()
        {
            foreach (KeyValuePair<string, string> PD in PluginsData)
            {
                string filename = PD.Key.Replace(" ", "") + ".cs";
                if (!File.Exists("oxide\\plugins\\" + filename))
                {
                    Puts("Installing Plugin " + filename);
                    File.WriteAllText("oxide\\plugins\\" + filename, PD.Value);
                }
                else
                {
                    if (!OverWrite)
                    {
                        Puts("Plugin " + PD.Key + " Already Installed");
                        return;
                    }
                    Puts("Overwritting Plugin " + filename);
                    File.WriteAllText("oxide\\plugins\\" + filename, PD.Value);
                }
            }
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

        object OnEntityKill(BaseNetworkable entity)
        {
            //Protects items from being destroyed.
            if (Protected.Contains(entity)) return true;
            return null;
        }

        [PluginReference]
        Plugin EntityScaleManager;

        public void Rescale()
        {
            //Checks if plugin is installed
            if (EntityScaleManager == null)
            {
                Puts(@"Scaling Disabled get plugin https://umod.org/plugins/entity-scale-manager");
                return;
            }
            int Scaled = 0;
            if (ServerSigns.Count != 0)
            {
                //Apply Scale Data to Found Signs
                foreach (KeyValuePair<Signage, Vector3> ss in ServerSigns)
                {
                    if (showDebug) Puts("Found Scaled Prefab @ " + ss.Key.transform.position + " : " + ss.Value.z.ToString());
                    foreach (KeyValuePair<Vector3, List<byte[]>> sd in SignData)
                    {
                        if (Vector3.Distance(sd.Key, ss.Key.transform.position) < 5)
                        {
                            //Scale
                            RemoveSphere(ss.Key);
                            if (doScale(ss.Key as BaseEntity, ss.Value.z))
                            {
                                ScaledEntitys.Add(ss.Key as BaseEntity);
                                Scaled++;
                                if (showDebug) Puts("Scaled to " + ss.Value.z.ToString());
                            }
                        }
                    }
                }
            }
            if (ServerSkinnables.Count != 0)
            {
                //Apply Scale Data to Found Skinnables
                foreach (KeyValuePair<BaseEntity, Vector3> ss in ServerSkinnables)
                {
                    if (showDebug) Puts("Found Scaled Prefab @ " + ss.Key.transform.position + " : " + ss.Value.z.ToString());
                    foreach (KeyValuePair<Vector3, uint> sd in SkinData)
                    {
                        if (Vector3.Distance(sd.Key, ss.Key.transform.position) < 5)
                        {
                            //Scale
                            RemoveSphere(ss.Key);

                            if (doScale(ss.Key, ss.Value.z))
                            {
                                ScaledEntitys.Add(ss.Key as BaseEntity);
                                Scaled++;
                                if (showDebug) Puts("Scaled to " + ss.Value.z.ToString());
                            }
                        }
                    }
                }
            }
            if (ServerScalable.Count != 0)
            {
                //Apply Scale Data to Found Scaleable
                foreach (KeyValuePair<BaseEntity, Vector3> ss in ServerScalable)
                {
                    if (showDebug) Puts("Found Scaled Prefab @ " + ss.Key.transform.position + " : " + ss.Value.z.ToString());
                    //Scale
                    RemoveSphere(ss.Key);

                    if (doScale(ss.Key, ss.Value.z))
                    {
                        ScaledEntitys.Add(ss.Key as BaseEntity);
                        Scaled++;
                        if (showDebug) Puts("Scaled to " + ss.Value.z.ToString());
                    }
                }
            }
            Puts("Scaled " + Scaled.ToString() + " Entitys");
        }

        private void Unload()
        {
            //Remove all scaling for clear start using map data.
            foreach (BaseEntity be in ScaledEntitys)
            {
                RemoveSphere(be);
            }
        }

        private void RemoveSphere(BaseEntity be)
        {
            var sphereEntity = be.GetParentEntity() as SphereEntity;
            if (sphereEntity == null)
            {
                return;
            }
            be.transform.localScale /= sphereEntity.currentRadius;
            be.SetParent(sphereEntity.GetParentEntity(), worldPositionStays: true, sendImmediate: true);
            sphereEntity.Kill();
        }
        void OnServerInitialized()
        {
            Startup();
        }

        public void Startup()
        {
            foreach (Signage Neon in UnityEngine.Object.FindObjectsOfType<Signage>())
            {
                if (Neons.Contains(Neon.prefabID) && Neon.OwnerID == 0)
                {
                    Neon.Kill();
                }
            }
            //Extract Map Data
            for (int i = World.Serialization.world.maps.Count - 1; i >= 0; i--)
            {
                MapData mapdata = World.Serialization.world.maps[i];
                if (mapdata.name == Base64Encode("SerializedImageData"))
                {
                    //Process ImageData
                    XMLDecode(System.Text.Encoding.UTF8.GetString(mapdata.data));
                    Puts("Processed SerializedImageData " + SignData.Count.ToString() + " Images Found");
                }
                else if (mapdata.name == Base64Encode("SerializedSkinData"))
                {
                    //Process SkinData
                    XMLDecodeSkin(System.Text.Encoding.UTF8.GetString(mapdata.data));
                    Puts("Processed SerializedSkinData " + SkinData.Count.ToString() + " Skins Found");
                }
            }
            int FixedNeons = 0;
            //Find All Server Signs and skinnables in the map file
            for (int i = World.Serialization.world.prefabs.Count - 1; i >= 0; i--)
            {
                PrefabData prefabdata = World.Serialization.world.prefabs[i];
                if (Neons.Contains(prefabdata.id))
                {
                    FixedNeons += CreateNeon(prefabdata);
                }
                if (isSign(prefabdata))
                {
                    foreach (Signage s in FindSign(prefabdata.position, 3f))
                    {
                        if (!ServerSigns.ContainsKey(s))
                            ServerSigns.Add(s, prefabdata.scale);
                    }
                }
                if (isSkinnable(prefabdata))
                {
                    foreach (BaseEntity s in FindSkin(prefabdata.position, 10f))
                    {
                        if (skinnableids.Contains(s.prefabID))
                        {
                            if (!ServerSkinnables.ContainsKey(s))
                            {
                                ServerSkinnables.Add(s, prefabdata.scale);
                            }
                        }
                    }
                }
                if (isScaleable(prefabdata))
                {
                    foreach (BaseEntity s in FindSkin(prefabdata.position, 10f))
                    {
                        if (ScaleableRE.Contains(s.prefabID) || skinnableids.Contains(s.prefabID))
                        {
                            if (!ServerScalable.ContainsKey(s))
                                ServerScalable.Add(s, prefabdata.scale);
                        }
                    }
                }
            }
            if (showDebug) Puts("Fixed " + FixedNeons.ToString() + " Neon Signs");
            if (showDebug) Puts("Found " + ServerSigns.Count.ToString() + " Server Signs");
            if (showDebug) Puts("Found " + ServerSkinnables.Count.ToString() + " Server Skinnable Items");
            if (showDebug) Puts("Found " + ServerScalable.Count.ToString() + " Server Scaleable Items");
            //Check if there is sign data
            if (ServerSigns.Count != 0)
            {
                //Apply Sign Data to Found Signs
                foreach (KeyValuePair<Signage, Vector3> ss in ServerSigns)
                {
                    if (showDebug) Puts("Found Sign @ " + ss.Key.transform.position);
                    foreach (KeyValuePair<Vector3, List<byte[]>> sd in SignData)
                    {
                        if (Vector3.Distance(sd.Key, ss.Key.transform.position) < 0.6)
                        {
                            if (showDebug) Puts("Applying Image");
                            if (sd.Value.Count == 1)
                            {
                                ApplySignage(ss.Key, sd.Value[0], 0);
                            }
                            else
                            {
                                for (int id = 0; id < sd.Value.Count; id++)
                                {
                                    try
                                    {
                                        ApplySignage(ss.Key, sd.Value[id], id);
                                    }
                                    catch { }
                                }
                            }
                            if (!Protected.Contains(ss.Key as BaseEntity))
                            {
                                Protected.Add(ss.Key as BaseEntity);
                            }
                            ss.Key.SetFlag(BaseEntity.Flags.Locked, true);
                            ss.Key.SendNetworkUpdate();
                        }
                    }
                }
            }
            if (ServerSkinnables.Count != 0)
            {
                //Apply Skin Data to Found Skinnables
                foreach (KeyValuePair<BaseEntity, Vector3> ss in ServerSkinnables)
                {
                    if (showDebug) Puts("Found skinnable @ " + ss.Key.transform.position);
                    foreach (KeyValuePair<Vector3, uint> sd in SkinData)
                    {
                        if (Vector3.Distance(sd.Key, ss.Key.transform.position) < 0.6)
                        {
                            if (showDebug) Puts("Applying skin");
                            ApplySkin(ss.Key, sd.Value);
                            if (!Protected.Contains(ss.Key as BaseEntity))
                            {
                                Protected.Add(ss.Key as BaseEntity);
                            }
                        }
                    }
                }
            }
            Rescale();
            foreach (BaseEntity meshdestroy in Protected)
            {
                DestroyMeshCollider(meshdestroy);
            }
        }

        public int CreateNeon(PrefabData pd)
        {
            //Create New Neon
            try
            {
                if (FindSign(pd.position, 0.5f).Count > 0)
                {
                    if (showDebug) Puts("Already A Neon There");
                    return 0;
                }

                NeonSign replacement = GameManager.server.CreateEntity(StringPool.Get(pd.id), pd.position, pd.rotation) as NeonSign;
                if (replacement == null) return 0;
                DestroyGroundComp(replacement);
                DestroyMeshCollider(replacement);
                Protected.Add(replacement);
                replacement.Spawn();
                replacement.currentFrame = 0;
                replacement.animationSpeed = 1f;
                replacement.transform.position = pd.position;
                replacement.transform.rotation = pd.rotation;
                replacement.pickup.enabled = false;
                byte[] Blank = Convert.FromBase64String(Blanked);
                if (replacement.prefabID == 708840119)
                {
                    ApplySignage(replacement, Blank, 0);
                    ApplySignage(replacement, Blank, 1);
                    ApplySignage(replacement, Blank, 2);
                    ApplySignage(replacement, Blank, 3);
                    ApplySignage(replacement, Blank, 4);
                }
                else if (replacement.prefabID == 3591916872)
                {
                    ApplySignage(replacement, Blank, 0);
                    ApplySignage(replacement, Blank, 1);
                    ApplySignage(replacement, Blank, 2);
                }
                else
                {
                    ApplySignage(replacement, Blank, 0);
                }

                //Give full power
                replacement.UpdateHasPower(100, 1);
                replacement.SendNetworkUpdateImmediate(true);
                return 1;
            }
            catch { }
            return 0;
        }

        public bool doScale(BaseEntity be, float radius)
        {
            //Scale
            if (EntityScaleManager != null)
            {
                //Dead zone for rounding
                if (radius > 1.1f || radius < 0.9f)
                {
                    //Sends Command to EntityScaleManager
                    EntityScaleManager.Call("API_ScaleEntity", be, radius);
                    return true;
                }
            }
            return false;
        }

        List<Signage> FindSign(Vector3 pos, float radius)
        {
            //Casts a sphere at given position and find all signs there
            var hits = Physics.SphereCastAll(pos, radius, Vector3.one);
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
            //Casts a sphere at given position and find all Skins there
            var hits = Physics.SphereCastAll(pos, radius, Vector3.one);
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
            if (!_signSizes.ContainsKey(sign.ShortPrefabName))
                return;

            var size = Math.Max(sign.paintableSources.Length, 1);
            if (sign.textureIDs == null || sign.textureIDs.Length != size)
            {
                Array.Resize(ref sign.textureIDs, size);
            }
            var resizedImage = ImageResize(imageBytes, _signSizes[sign.ShortPrefabName].Width,
                _signSizes[sign.ShortPrefabName].Height);
            //Applys Image
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
        bool XMLDecode(string SerialData)
        {
            if (!SerialData.Contains("xml version")) return false;
            string[] DataParse = SerialData.Split(new string[] { "<position>" }, StringSplitOptions.None);
            foreach (string xmldata in DataParse)
            {
                if (xmldata.Contains("xml version")) continue;
                try
                {
                    string x = xmldata.Split(new string[] { "</x><y>" }, StringSplitOptions.None)[0].Replace("<x>", "");
                    string y = xmldata.Split(new string[] { "</y><z>" }, StringSplitOptions.None)[0].Replace("<x>" + x + "</x><y>", "");
                    string z = xmldata.Split(new string[] { "</z></position>" }, StringSplitOptions.None)[0].Replace("<x>" + x + "</x><y>" + y + "</y><z>", "");
                    string texture = xmldata.Split(new string[] { "<texture>" }, StringSplitOptions.None)[1].Replace("</texture>", "").Replace("</SerializedImageData>", "");
                    string[] imageFrames = texture.Split(new string[] { "<frame>" }, StringSplitOptions.None);
                    List<byte[]> ImageData = new List<byte[]>();
                    foreach (string imageframe in imageFrames)
                    {
                        if (imageframe != "")
                        {
                            ImageData.Add(Convert.FromBase64String(imageframe.Replace("<frame>", "")));
                        }
                        else
                        {
                            ImageData.Add(Convert.FromBase64String(Blanked));
                        }
                    }
                    Vector3 pos = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
                    if (!SignData.ContainsKey(pos))
                    {
                        try
                        {
                            SignData.Add(pos, ImageData);
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return true;
        }

        bool XMLDecodePlugins(string PluginData)
        {
            if (!PluginData.Contains("xml version")) return false;
            string[] DataParse = PluginData.Split(new string[] { "<name>" }, StringSplitOptions.None);
            foreach (string xmldata in DataParse)
            {
                if (xmldata.Contains("xml version")) continue;
                try
                {
                    string Name = xmldata.Split(new string[] { "</name>" }, StringSplitOptions.None)[0];
                    string Data = xmldata.Split(new string[] { "</data>" }, StringSplitOptions.None)[0].Split(new string[] { "<data>" }, StringSplitOptions.None)[1];
                    if (!PluginsData.ContainsKey(Name))
                    {
                        PluginsData.Add(Name, Base64Decode(Data));
                    }
                }
                catch { }
            }
            return true;
        }

        bool XMLDecodeSkin(string SerialData)
        {
            if (!SerialData.Contains("xml version")) return false;
            string[] DataParse = SerialData.Split(new string[] { "<position>" }, StringSplitOptions.None);
            foreach (string xmldata in DataParse)
            {
                if (xmldata.Contains("xml version")) continue;
                try
                {
                    string x = xmldata.Split(new string[] { "</x><y>" }, StringSplitOptions.None)[0].Replace("<x>", "");
                    string y = xmldata.Split(new string[] { "</y><z>" }, StringSplitOptions.None)[0].Replace("<x>" + x + "</x><y>", "");
                    string z = xmldata.Split(new string[] { "</z></position>" }, StringSplitOptions.None)[0].Replace("<x>" + x + "</x><y>" + y + "</y><z>", "");
                    uint skinid = uint.Parse(xmldata.Split(new string[] { "<skin>" }, StringSplitOptions.None)[1].Replace("</skin>", "").Replace("</SerializedSkinData>", ""));
                    Vector3 pos = new Vector3(float.Parse(x), float.Parse(y), float.Parse(z));
                    SkinData.Add(pos, skinid);
                }
                catch { }
            }
            return true;
        }

        //Create XML Data
        string XMLEncode()
        {
            string XMLData = @"<? xml version=""1.0""?><SerializedImageData>";
            string SerialData = "";
            foreach (KeyValuePair<Signage, Vector3> _sign in ServerSigns)
            {
                SerialData += ("<position>" +
                                   "<x>" + _sign.Key.transform.position.x.ToString("0.0") + "</x>" +
                                   "<y>" + _sign.Key.transform.position.y.ToString("0.0") + "</y>" +
                                   "<z>" + _sign.Key.transform.position.z.ToString("0.0") + "</z>" +
                                   "</position>" +
                                   "<texture>");
                List<byte[]> Images = new List<byte[]>();
                for (int ids = 0; ids < _sign.Key.textureIDs.Length; ids++)
                {
                    try
                    {
                        byte[] image = FileStorage.server.Get(_sign.Key.textureIDs[ids], FileStorage.Type.png, _sign.Key.net.ID);
                        Images.Add(image);
                    }
                    catch
                    {
                        Images.Add(Convert.FromBase64String(Blanked));
                    }
                }
                foreach (byte[] imagedata in Images)
                {
                    try
                    {
                        SerialData += Convert.ToBase64String(imagedata) + "<frame>";
                    }
                    catch
                    {
                        SerialData += Blanked + "<frame>";
                    }
                }
                SerialData += "</texture>";
            }
            XMLData = XMLData + SerialData + "</SerializedImageData>";
            return XMLData;
        }

        string XMLEncodeSkin()
        {
            string XMLData = @"<? xml version=""1.0""?><SerializedSkinData>";
            string SerialData = "";
            foreach (KeyValuePair<BaseEntity, Vector3> _skin in ServerSkinnables)
            {
                if (_skin.Key.skinID != 0)
                {
                    SerialData += ("<position>" +
                               "<x>" + _skin.Key.transform.position.x.ToString("0.0") + "</x>" +
                               "<y>" + _skin.Key.transform.position.y.ToString("0.0") + "</y>" +
                               "<z>" + _skin.Key.transform.position.z.ToString("0.0") + "</z>" +
                                   "</position>" +
                                   "<skin>" +
                                   _skin.Key.skinID.ToString() +
                                   "</skin>");
                }
            }
            XMLData = XMLData + SerialData + "</SerializedSkinData>";
            return XMLData;
        }

        //Finds Signs
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
            return sign != null;
        }

        //Image Downloading Thanks SignArtist
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

        private void StartNextDownload(bool reduceCount = false)
        {
            try
            {
                ServerMgr.Instance.StartCoroutine(DownloadImage(downloadQueue.Dequeue()));
            }
            catch { }
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
            int fselected = 0;
            if (request.Url.StartsWith("frame:0"))
            {
                fselected = 0;
                request.Url = request.Url.Replace("frame:0", "");
            }
            else if (request.Url.StartsWith("frame:1"))
            {
                fselected = 1;
                request.Url = request.Url.Replace("frame:1", "");
            }
            else if (request.Url.StartsWith("frame:2"))
            {
                fselected = 2;
                request.Url = request.Url.Replace("frame:2", "");
            }
            else if (request.Url.StartsWith("frame:3"))
            {
                fselected = 3;
                request.Url = request.Url.Replace("frame:3", "");
            }
            else if (request.Url.StartsWith("frame:4"))
            {
                fselected = 4;
                request.Url = request.Url.Replace("frame:4", "");
            }
            byte[] imageBytes;
            //Path for Base64 weblinks
            if (request.Url.StartsWith("data:image"))
            {
                imageBytes = LoadImage(request.Url);
            }
            else
            {
                UnityWebRequest www = UnityWebRequest.Get(request.Url);

                yield return www.SendWebRequest();
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
            request.Sign.SetImage(FileStorage.server.Store(resizedImageBytes, FileStorage.Type.png, request.Sign.NetId), fselected);
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
            //Convert Base64 link image to data.
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

        void DestroyGroundComp(BaseEntity ent)
        {
            UnityEngine.Object.DestroyImmediate(ent.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(ent.GetComponent<GroundWatch>());
            //Stops Decay
            UnityEngine.Object.DestroyImmediate(ent.GetComponent<DeployableDecay>());
        }

        void DestroyMeshCollider(BaseEntity ent)
        {
            foreach (var mesh in ent.GetComponentsInChildren<MeshCollider>())
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        //Chat Commands
        //
        //Prints Image Onto Sign being looked at.
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
            //Sets Skin
            entity.skinID = skin;
            entity.SendNetworkUpdateImmediate();
            basePlayer.ChatMessage("Applying Skin");
        }

        //Scale Items that are being looked at.
        [ChatCommand("sscale")]
        private void ScaleCommand(BasePlayer basePlayer, string command, string[] args)
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
            if (EntityScaleManager == null)
            {
                Puts(@"Scaling Disabled get plugin https://umod.org/plugins/entity-scale-manager");
                return;
            }
            float scale;
            if (args.Length != 1 || !float.TryParse(args[0], out scale))
            {
                basePlayer.ChatMessage("Invalid Scale");
                return;
            }

            RaycastHit hit;
            if (!Physics.Raycast(basePlayer.eyes.HeadRay(), out hit))
            {
                basePlayer.ChatMessage("No Scalable Entitys Found. Try Looking at the Hinge area if its a door.");
                return;
            }

            var entity = hit.GetEntity();
            if (entity == null)
            {
                basePlayer.ChatMessage("No Scalable Entitys Found. Try Looking at the Hinge area if its a door.");
                return;
            }

            //Send scale command to EntityScaleManager
            EntityScaleManager.Call("API_ScaleEntity", entity, scale);

            //Find in prefab list and update its scale
            for (int i = 0; i < World.Serialization.world.prefabs.Count; i++)
            {
                if (entity.transform.position == World.Serialization.world.prefabs[i].position)
                {
                    World.Serialization.world.prefabs[i].scale.z = scale;
                    basePlayer.ChatMessage("Updated in Map Prefab Data");
                }
            }
        }


        //Read plugins out of MapData then install them into plugin folder.
        [ConsoleCommand("sinstall")]
        void InstallPlugins(ConsoleSystem.Arg arg)
        {
            for (int i = World.Serialization.world.maps.Count - 1; i >= 0; i--)
            {
                MapData mapdata = World.Serialization.world.maps[i];
                if (mapdata.name == Base64Encode("SerializedPluginData"))
                {
                    //Process Plugins
                    XMLDecodePlugins(System.Text.Encoding.UTF8.GetString(mapdata.data));
                    Puts("Processed SerializedPluginData " + PluginsData.Count.ToString() + " Plugins Found");
                    LoadPlugins();
                }
            }
        }

        //Resets to settings in mapdata
        [ChatCommand("sreset")]
        void Reset(BasePlayer player, string command, string[] args)
        {
            //Remove Protection
            Protected.Clear();
            //Remove all scaled entitys.
            foreach (BaseEntity be in ScaledEntitys)
            {
                try
                {
                    be.OwnerID = 123456;
                    be.AdminKill();
                }
                catch { }
            }

            //Scan though prefab list and remove skinnable items and paintable signs placed in rustedit.
            foreach (PrefabData pd in World.Serialization.world.prefabs)
            {
                if (signids.Contains(pd.id))
                {
                    BaseEntity[] BaseEntity = FindSign(pd.position, 3f).ToArray();
                    foreach (BaseEntity be in BaseEntity)
                    {
                        if (be != null)
                        {
                            be.OwnerID = 123456;
                            be.AdminKill();
                        }
                    }
                }
                if (skinnableids.Contains(pd.id))
                {
                    BaseEntity[] BaseEntity = FindSkin(pd.position, 10f).ToArray();
                    foreach (BaseEntity be in BaseEntity)
                    {
                        if (be != null)
                        {
                            be.OwnerID = 123456;
                            be.AdminKill();
                        }
                    }
                }
            }
            player.ChatMessage("Removed server skinnables and signs.");
            //Delay to allow everything to be destroyed.
            timer.Once(5f, () =>
            {
                player.ChatMessage("Respawning server skinnables and signs.");
                //Recreate them from Prefab List
                foreach (PrefabData pd in World.Serialization.world.prefabs)
                {
                    if (signids.Contains(pd.id))
                    {
                        Signage replacement = GameManager.server.CreateEntity(StringPool.Get(pd.id), pd.position, pd.rotation) as Signage;
                        if (replacement == null) return;
                        DestroyGroundComp(replacement);
                        DestroyMeshCollider(replacement);
                        Protected.Add(replacement);
                        replacement.Spawn();
                        replacement.transform.position = pd.position;
                        replacement.transform.rotation = pd.rotation;
                        replacement.pickup.enabled = false;
                        replacement.SendNetworkUpdateImmediate(true);
                    }
                    if (skinnableids.Contains(pd.id))
                    {
                        string isdoor = StringPool.Get(pd.id);
                        if (isdoor.Contains("hinged"))
                        {
                            Door replacement = GameManager.server.CreateEntity(StringPool.Get(pd.id), pd.position, pd.rotation) as Door;
                            if (replacement == null) return;
                            DestroyMeshCollider(replacement);
                            DestroyGroundComp(replacement);
                            Protected.Add(replacement);
                            replacement.Spawn();
                            replacement.transform.position = pd.position;
                            replacement.transform.rotation = pd.rotation;
                            replacement.grounded = true;
                            replacement.pickup.enabled = false;
                            replacement.SendNetworkUpdateImmediate(true);
                        }
                        else
                        {
                            BaseEntity replacement = GameManager.server.CreateEntity(StringPool.Get(pd.id), pd.position, pd.rotation) as BaseEntity;
                            if (replacement == null) return;
                            DestroyGroundComp(replacement);
                            DestroyMeshCollider(replacement);
                            Protected.Add(replacement);
                            replacement.Spawn();
                            replacement.transform.position = pd.position;
                            replacement.transform.rotation = pd.rotation;
                            replacement.SendNetworkUpdateImmediate(true);
                        }
                    }
                }
                //Restarting Plugin
                player.ChatMessage("Restarting Plugin in 5s");
                timer.Once(5f, () =>
                {
                    covalence.Server.Command("o.reload", this.Name);
                });
            });
        }

        //Chat Command to remove skinnable and paintable entitys since they are given protection.
        [ChatCommand("sremove")]
        void removeentity(BasePlayer player, string command, string[] args)
        {
            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out hit))
            {
                player.ChatMessage("No Entitys Found. Try Looking at the Hinge area if its a door.");
                return;
            }

            var entity = hit.GetEntity();
            if (entity == null)
            {
                player.ChatMessage("No Entitys Found. Try Looking at the Hinge area if its a door.");
                return;
            }
            if (Protected.Contains(entity))
            {
                Protected.Remove(entity);
                entity.OwnerID = 123456;
                entity.Kill();
                player.ChatMessage("Entity protection disabled and removed.");
                return;
            }
            player.ChatMessage("Not a protected entity use normal admin kill on it.");
        }

        //Save Map and Data to MapData
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
            //Create File
            World.Serialization.Save(mapname);
            player.ChatMessage("Saved edited map in root dir as " + mapname);
        }
    }
}
