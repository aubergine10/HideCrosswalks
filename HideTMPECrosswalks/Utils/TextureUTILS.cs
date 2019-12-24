using UnityEngine;

using System.Diagnostics;
using System.IO;
using System.Threading;
using System;
using System.Linq;

namespace HideTMPECrosswalks.Utils {
    using static HideTMPECrosswalks.Utils.ColorUtils;
    public static class TextureUtils {
        public delegate Texture2D TProcessor(Texture2D tex);
        public delegate Texture2D TProcessor2(Texture2D tex, Texture2D tex2);

        public static class DumpJob {
            public static bool Lunch(NetInfo info) =>
                ThreadPool.QueueUserWorkItem((_) => Dump(info));

            static object locker = new object();
            public static void Dump(NetInfo info) {
                // not intersted in multi-thread processing. I just wan it to be outside of simulation thread.
                lock (locker) {
                    string roadName = info.GetUncheckedLocalizedTitle();
                    //for (int i = 0; i < info.m_segments.Length; ++i)
                    {
                        int i = 0;
                        var seg = info.m_segments[i];
                        Material material = seg.m_segmentMaterial;
                        string baseName = "segment";
                        //if (info.m_segments.Length > 1) baseName += i;
                        Dump(material, "_MainTex", baseName, dir:roadName);
                        //Dump(material, "_ARPMap", baseName, dir);
                        //try { Dump(material, "_XYSMap", baseName, dir); } catch { }
                    }
                    for (int i = 0; i < info.m_nodes.Length; ++i) {
                        var node = info.m_nodes[i];
                        Material material = node.m_nodeMaterial;
                        string baseName = "node";
                        if (info.m_nodes.Length > 1) baseName += i;
                        Dump(material, baseName, dir: roadName);

                    }
                }
            }

            public static void Dump(Material material, string baseName=null, string dir=null) {
                if (dir == null) dir = "dummy";
                if (baseName == null) baseName = material.name;
                Dump(material, TextureNames.Defuse, baseName, dir);
                Dump(material, TextureNames.AlphaMAP, baseName, dir);
                try { Dump(material, TextureNames.XYSMap, baseName, dir); } catch { }

            }

            public static void Dump(Material material, string texName, string baseName, string dir) {
                Extensions.Log($"BEGIN : {texName} {baseName} {dir}");
                Texture2D texture = material.GetReadableTexture(texName);
                Extensions.Log($"Dumping texture " + texture.name);
                byte[] bytes = texture.EncodeToPNG();

                string path = @"mod debug dumps\" + dir;
                Directory.CreateDirectory(path);
                path += @"\" + baseName + texName + ".png";
                File.WriteAllBytes(path, bytes);
                Extensions.Log($"END: {texName} {baseName} {dir}");
            }
        }

        public static class TextureNames {
            public static string Defuse => "_MainTex";
            public static string AlphaMAP => "_APRMap";
            public static string XYSMap => "_XYSMap"; // TODO is this correct?

            public static string[] Names => new[] { Defuse, AlphaMAP, XYSMap };

        }

        public static Texture2D GetReadableTexture(this Material material, string name = "_MainTex") {
            if (!TextureNames.Names.Contains(name))
                throw new Exception($"{name} bad texture name. valid texture names are" + String.Join(" ", TextureNames.Names));
            Texture2D texture = material.GetTexture(name) as Texture2D;
            Texture2D texture2 = texture.MakeReadable();
            Extensions.Log($"GetReadableTexture: Material={material.name} texture={texture.name}");
            return texture2;
        }

        public static Texture2D TryMakeReadable(this Texture tex) {
            Extensions.Log($"TryMakeReadable:  texture={tex.name}");
            try {
                return tex.MakeReadable() as Texture2D;
            } catch {
                return tex as Texture2D;
            }
        }

        public static void Process(Material material, string name, TProcessor func) {
            Texture2D texture = material.GetReadableTexture(name);
            Texture2D newTexture = func(texture);
            newTexture.anisoLevel = texture.anisoLevel;
            newTexture.Compress(true);
            material.SetTexture(name, newTexture);
        }

        public static void Process(Material nodeMaterial, Material segmentMaterial, string name, TProcessor2 func) {
            Texture2D nodeTex = nodeMaterial.GetReadableTexture(name);
            Texture2D segTex = segmentMaterial.GetReadableTexture(name);

            Texture2D newTexture = func(nodeTex,segTex);
            newTexture.anisoLevel = nodeTex.anisoLevel;
            newTexture.Compress(true);
            nodeMaterial.SetTexture(name, newTexture);
        }

        public static Texture2D Process(Texture tex, TProcessor func) {
            Texture2D newTexture = tex.TryMakeReadable();
            newTexture = func(newTexture);
            newTexture.anisoLevel = tex.anisoLevel;
            newTexture.Compress(true);
            newTexture.name = tex.name + "-processed";
            return newTexture;
        }

        public static Texture2D Process(Texture tex, Texture tex2, TProcessor2 func) {
            Texture2D nodeTex = tex.TryMakeReadable();
            Texture2D segTex = tex2.TryMakeReadable();
            Texture2D newTexture = func(nodeTex, segTex);
            newTexture.anisoLevel = nodeTex.anisoLevel;
            newTexture.Compress(true);
            newTexture.name = tex.name + "-processed";
            return newTexture;
        }

        //blend tex2 into tex
        public static Texture2D MeldBlue(Texture2D tex, Texture2D tex2) {
            Texture2D ret = new Texture2D(tex.width, tex.height);
            int yM = (int)(tex.height * 0.4f);
            int yM2 = tex2.height / 2;

            Color[] colors2 = tex2.GetPixels(0, yM2, tex2.width, 1);
            {
                Color barrier = new Color(0, 1, 0, 1);
                Color noBarrier = new Color(0, 1, .8f, 1);
                float  blue_prev = 0; ;
                for (int i = 0; i < colors2.Length; ++i) {
                    float blue = colors2[i].b;
                    if (blue_prev - blue > 0.05f) {
                        blue = blue_prev - 0.005f;
                        if (blue < 0.9f) blue = 0.9f;
                    }
                    colors2[i].b = blue;
                    blue_prev = blue;

                }
                for (int i = colors2.Length - 1; i >= 0; --i) {
                    float blue = colors2[i].b;
                    if (blue_prev - blue > 0.05f) {
                        blue = blue_prev - 0.005f;
                        if (blue < 0.9f) blue = 0.9f;
                    }
                    colors2[i].b = blue;
                    blue_prev = blue;
                }
            }

            float ratio = (float)tex2.width / tex.width;
            for (int j = 0; j < yM; j++) {
                Color []colors  = tex .GetPixels(0, j, tex.width, 1);
                float w = (float)j / (float)yM;
                for (int i = 0; i < colors.Length; ++i) {
                    int i2 = (int)(i * ratio);
                    float blue = colors[i].b;
                    float blue2 = colors2[i2].b;
                    blue = blue * w + blue2 * (1f - w);
                    colors[i].b = blue;
                }
                ret.SetPixels(0, j, tex.width, 1, colors);
            }
            ret.SetPixels(0, yM, tex.width, tex.height - yM, tex.GetPixels(0, yM, tex.width, tex.height - yM));

            ret.Apply();
            return ret;
        }


        public static Texture2D MeldDiff(Texture2D tex, Texture2D tex2) {
            Texture2D ret = new Texture2D(tex.width, tex.height);
            int yM = (int)(tex.height * 0.3f);
            float ratio = (float)tex2.width / tex.width;

            Color[] colors = tex.GetPixels(0, 0, tex.width, 1);
            Color[] diff = tex2.GetPixels(0, tex2.height / 2, tex2.width, 1);
            diff.Subtract(colors);

            // TODO: which color values should I clamp or smoothen?
            diff.SmoothenAPR();
            diff.Clamp();

            for (int j = 0; j < yM; j++) {
                colors = tex.GetPixels(0, j, tex.width, 1);
                float w = 1f - (float)j / (float)yM;
                for (int i = 0; i < colors.Length; ++i) {
                    int i2 = (int)(i * ratio);
                    colors[i] += diff[i2] * w;
                }
                ret.SetPixels(0, j, tex.width, 1, colors);
            }
            ret.SetPixels(0, yM, tex.width, tex.height - yM, tex.GetPixels(0, yM, tex.width, tex.height - yM));

            ret.Apply();
            return ret;
        }

        public static Texture2D Crop(Texture2D original) {
            Texture2D ret = new Texture2D(original.width, original.height);

            int xN = original.width;
            int yN = original.height;

            float cropPortion = 0.30f;
            float stretchPortion = 0.40f;
            int yN0 = (int)(yN * stretchPortion);

            for (int j = 0; j < yN0; j++) {
                int j2 = (int)(j * (stretchPortion - cropPortion) + yN * cropPortion);
                ret.SetPixels(0, j, xN, 1, original.GetPixels(0, j2, xN, 1));
            }
            ret.SetPixels(0, yN0, xN, yN - yN0, original.GetPixels(0, yN0, xN, yN - yN0));

            ret.Apply();
            return ret;
        }

#if OLD_CODE
        ///CropOld => Crop where stretch=1f
        // I chose to write repeated code for the sake of simplicity and ease debugging.
        public static Texture2D CropOld(Texture2D original) {
            Texture2D ret = new Texture2D(original.width, original.height);

            int xN = original.width;
            int yN = original.height;

            float cropPortion = 0.30f;
            //float stretchPortion = 1f;
            //int yN0 = (int)(yN * stretchPortion) = yN;

            for (int j = 0; j < yN; j++) {
                int j2 = (int)(j * (1 - cropPortion) + yN * cropPortion);
                ret.SetPixels(0, j, xN, 1, original.GetPixels(0, j2, xN, 1));
            }

            ret.Apply();
            return ret;
        }

        //Texture flipping script from https://stackoverflow.com/questions/35950660/unity-180-rotation-for-a-texture2d-or-maybe-flip-both
        public static Texture2D Flip(Texture2D original) {
            Texture2D ret = new Texture2D(original.width, original.height);

            int xN = original.width;
            int yN = original.height;
            for (int i = 0; i < xN; i++) {
                for (int j = 0; j < yN; j++) {
                    ret.SetPixel(j, xN - i - 1, original.GetPixel(j, i)); // flip upside down
                }
            }

            ret.Apply();
            return ret;
        }



        public static Texture2D CropEnd(Texture2D original) {
            Texture2D ret = new Texture2D(original.width, original.height);

            int xN = original.width;
            int yN = original.height;

            float cropPortion = 0.20f;
            float stretchPortion = 0.30f;
            int yN0 = (int)(yN * (1- stretchPortion));


            ret.SetPixels(0, 0, xN, yN, original.GetPixels(0, 0, xN, yN));
            for (int j = 0; j < yN-yN0; j++) {
                int j2 = (int)(j * (stretchPortion - cropPortion)) + yN0;
                ret.SetPixels(0, j, xN,1, original.GetPixels(0, j2,xN,1));
            }

            ret.Apply();
            return ret;
        }

                public static Color MedianColor = new Color(0.106f, 0.098f, 0.106f, 1.000f);
        public static Color GetMedianColor(Material material) {
            var ticks = System.Diagnostics.Stopwatch.StartNew();

            Texture2D texture = material.GetReadableTexture(TextureNames.Defuse);
            int xN = texture.width;
            int yN = texture.height;
            //int yN0 = (int)(yN * 3)/10; //yN*30%

            Color[] colors = texture.GetPixels(0, yN / 2, xN, 1);
            // sort the colors.
            //int Compare(Color c1, Color c2) => Math.Sign(c1.grayscale - c2.grayscale);
            //Array.Sort(colors, Compare);

            Color ret = colors[xN / 2]; // middle
            ticks.LogLap($"GetMedianColor : middle = {ret} ");
            Extensions.Log("GetMedianColor : min = " + colors[0]);

            return ret;
        }

        public static void SetMedianColor(Material material, Color ?color=null) {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color ?? MedianColor);
            tex.Apply();
            material.SetTexture("_MainTex", tex);
        }
#endif // OLD_CODE
    }
}
