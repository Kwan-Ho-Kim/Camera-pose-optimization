using OpenCvSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace CameraOptimization
{
    public enum fitness_method
    {
        TemplateMatching,
        SIFT,
        SURF,
        ORB
    }

    public static class Utilities
    {
        public static Texture2D RenderToTexture2D(this RenderTexture rTex)
        {
            Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.RGB24, false);
            RenderTexture.active = rTex;
            tex.ReadPixels(new UnityEngine.Rect(0, 0, rTex.width, rTex.height), 0, 0);
            tex.Apply();
            return tex;
        }

        public static Mat RenderToMat(this RenderTexture rTex)
        {
            var tex = RenderToTexture2D(rTex);
            Mat img = OpenCvSharp.Unity.TextureToMat(tex);
            return img;
        }

        public static Mat RenderToEdge(this RenderTexture rTex)
        {
            var tex = RenderToTexture2D(rTex);
            Mat img = OpenCvSharp.Unity.TextureToMat(tex);
            img = img.Canny(0, 300);
            return img;
        }

        public static void SortWithIndices<T, U>(this List<T> fitness, List<U> follower) where T : IComparable<T>
        {
            for (int i = 1; i < fitness.Count; i++)
            {
                T key = fitness[i];
                int j = i - 1;
                while (j >= 0 && key.CompareTo(fitness[j]) < 0)     // key < fitness[j]
                {
                    Swap<T>(fitness, j, j + 1);
                    Swap<U>(follower, j, j + 1);
                    j--;
                }
            }
        }

        public static void Swap<T>(this List<T> list, int from, int to)
        {
            T tmp = list[from];
            list[from] = list[to];
            list[to] = tmp;
        }

        public static param RandParam(float p_min, float p_max, float r_min, float r_max, float fov_min, float fov_max)
        {
            Vector3 rand_pos = new Vector3(UnityEngine.Random.Range(p_min, p_max), UnityEngine.Random.Range(p_min, p_max), UnityEngine.Random.Range(p_min, p_max));
            Vector3 rand_rot = new Vector3(UnityEngine.Random.Range(r_min, r_max), UnityEngine.Random.Range(r_min, r_max), UnityEngine.Random.Range(r_min, r_max));
            float rand_fov = UnityEngine.Random.Range(fov_min, fov_max);

            param param = new param();
            param.pose = rand_pos;
            param.euler_rot = rand_rot;
            param.quat_rot = Quaternion.Euler(rand_rot);
            param.fov = rand_fov;

            return param;
        }

        public static param RandParam(Vector3 pos, Vector3 rot, float fov)
        {
            Vector3 rand_pos = new Vector3(UnityEngine.Random.Range(-pos.x,pos.x), UnityEngine.Random.Range(-pos.y,pos.y), UnityEngine.Random.Range(-pos.z, pos.z));
            Vector3 rand_rot = new Vector3(UnityEngine.Random.Range(-rot.x, rot.x), UnityEngine.Random.Range(-rot.y, rot.y), UnityEngine.Random.Range(-rot.z, rot.z));
            float rand_fov = UnityEngine.Random.Range(-fov, fov);

            param param = new param();
            param.pose = rand_pos;
            param.euler_rot = rand_rot;
            param.quat_rot = Quaternion.Euler(rand_rot);
            param.fov = rand_fov;

            return param;
        }
        
        public static int RandomIndexByFitness(this List<CamParameters> cam_list, int except_idx = -1, float bias = 10)
        {
            float randf= UnityEngine.Random.Range(0f, 1f);
            float[] fits = new float[cam_list.Count];
            float min_fit = float.MaxValue;
            for(int i = 0;i<cam_list.Count;i++)
            {
                fits[i] = cam_list[i].fitness;
                if (fits[i] == float.MinValue)
                {
                    return cam_list.Count-1;
                }
                if (min_fit > fits[i])
                {
                    min_fit = fits[i];
                }
            }
            float fit_sum = 0f;
            for(int i = 0; i< fits.Length;i++)
            {
                fits[i] -= min_fit;
                fits[i] += bias;
                if (except_idx == i) fits[i] = 0;
                fit_sum += fits[i];
            }

            float cumulative_sum = 0;
            int idx = 0;
            foreach(float f in fits)
            {
                cumulative_sum += f/fit_sum;
                if (cumulative_sum > randf)
                    break;
                idx++;
            }

            return idx;
        }

        public static void SaveInformation(this List<CamParameters> cam_list, string info, string file_path = null)
        {
            info = "===================\n" + info + "\n";
            foreach (CamParameters cam_param in cam_list)
            {
                info += "\ncam name : "+cam_param.cam_tf.name;
                info += "\npos : "+cam_param.param.pose;
                info += "\nrot : " + cam_param.param.euler_rot;
                info += "\nfov : " + cam_param.param.fov;
                info += "\ndistance loss : " + cam_param.dist_loss;
                info += "\nmatch num : " + cam_param.match_num;
                info += "\nfitness : " + cam_param.fitness+"\n";
            }

            info += "===================\n";
            Debug.Log(info);

            if(file_path != null)
            {
                StreamWriter sw = new StreamWriter(file_path);
                sw.WriteLine(info);
                sw.Flush();
                sw.Close();
            }
        }

        //public static void TakeSnapShotAndSave()
        //{
        //    //Get the corners of RectTransform rect and store it in a array vector
        //    Vector3[] corners = new Vector3[4];
        //    RectTransform _objToScreenshot = Image.GetComponent<RectTransform>();
        //    _objToScreenshot.GetWorldCorners(corners);


        //    Vector3[] worldToScreenPointCorners = new Vector3[4];
        //    for (int i = 0; i < corners.Length; i++)
        //    {
        //        Vector3 screenPoint = Camera.WorldToScreenPoint(corners[i]);

        //        /*Vector2 result;
        //        RectTransformUtility.ScreenPointToLocalPointInRectangle(transform.parent.GetComponent<RectTransform>(), screenPoint, Camera, out result);*/

        //        //worldToScreenPointCorners[i]= new Vector3(result.x, result.y, 0f);
        //        worldToScreenPointCorners[i] = screenPoint;
        //    }

        //    /*
        //    RectTransform mCanvas = Canvs.GetComponent<RectTransform>();
        //    float scalerX = mCanvas.rect.width / (float)Camera.pixelWidth;//(float)Screen.width / 1080f;
        //    float scalerY = mCanvas.rect.height / (float)Camera.pixelHeight;//(float)Screen.height / 1920f;
        //    */
        //    float scalerX = GetScale(Screen.width, Screen.height, scaler.referenceResolution, scaler.matchWidthOrHeight);
        //    float scalerY = GetScale(Screen.width, Screen.height, scaler.referenceResolution, scaler.matchWidthOrHeight);

        //    //Remove 100 and you will get error
        //    int width = (int)(_objToScreenshot.rect.width * scalerX);//((int)corners[3].x - (int)corners[0].x) - 100;
        //    int height = (int)(_objToScreenshot.rect.height * scalerY);// (int)corners[1].y - (int)corners[0].y;
        //    /* int width = ((int)worldToScreenPointCorners[3].x - (int)worldToScreenPointCorners[0].x);
        //     int height =  (int)worldToScreenPointCorners[1].y - (int)worldToScreenPointCorners[0].y;*/

        //    var startX = worldToScreenPointCorners[0].x;
        //    var startY = worldToScreenPointCorners[0].y;

        //    //Make a temporary texture and read pixels from it
        //    Rect pixelsRect = new Rect(startX, startY, width, height);
        //    //Rect pixelsRect = new Rect(startX , startY , width , height);
        //    Texture2D ss = new Texture2D(width, height, TextureFormat.RGB24, false);
        //    ss.ReadPixels(pixelsRect, 0, 0);
        //    ss.Apply();

        //    Debug.Log("Start X : " + startX + " Start Y : " + startY);
        //    Debug.Log("Screen Width : " + Screen.width + " Screen Height : " +
        //    Screen.height);
        //    Debug.Log("Texture Width : " + width + " Texture Height : " + height);


        //    Debug.Log($"Draw Rect : {pixelsRect}");

        //    //Save the screenshot to disk
        //    byte[] byteArray = ss.EncodeToPNG();
        //    string savePath = Application.persistentDataPath + "/ScreenshotSave.png";
        //    System.IO.File.WriteAllBytes(savePath, byteArray);
        //    Debug.Log("Screenshot Path : " + savePath);

        //    // Destroy texture to avoid memory leaks
        //    if (Application.isPlayer)
        //        Destroy(ss);
        //}
        //private float GetScale(int width, int height, Vector2 scalerReferenceResolution, float scalerMatchWidthOrHeight)
        //{
        //    return Mathf.Pow(width / scalerReferenceResolution.x, 1f - scalerMatchWidthOrHeight) *
        //           Mathf.Pow(height / scalerReferenceResolution.y, scalerMatchWidthOrHeight);
        //}
    }
}
