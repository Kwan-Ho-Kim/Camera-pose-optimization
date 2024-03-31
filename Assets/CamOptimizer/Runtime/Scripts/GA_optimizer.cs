using OpenCvSharp;
using OpenCvSharp.XFeatures2D;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using CameraOptimization;
using System.IO;
using UnityEngine.SceneManagement;
//using static UnityEditor.ShaderData;
//using static UnityEngine.GraphicsBuffer;
//using static Unity.VisualScripting.Member;
//using Unity.VisualScripting;
//using System.Text.RegularExpressions;
//using System.Threading;
//using System.ComponentModel;

public class GA_optimizer : MonoBehaviour
{
    [Header("Camera")]
    private RenderTexture cam_rt;
    private Camera cam;
    public Transform cam_tf;

    [Header("UI")]
    public RawImage gt_image;
    public RawImage best_image;

    [Header("GA parameters")]
    public int num_iteration = 300;
    public int num_population = 50;
    public int num_exchange = 20;

    public float PosRandomRange = 5f;
    public float RotRandomRange = 10f;
    public float FovRandomRange = 10f;

    public float PosExpandRatio = 2;
    public float RotExpandRatio = 2;
    public float FovExpandRatio = 2;

    public float child_mutation_prob = 0.5f;
    public float component_mutation_prob = 0.5f;

    public float FeatureFilterRatio = 0.1f;
    public float alpha = 0.6f;
    public float OverfitThres = 20f;

    private int iter = 0;
    private int episode = 0;
    private int local_min_change_num = 0;

    [Header("Utils")]
    public Vector3 start_pos = new Vector3(-18.23f, 28.56f, -9.57f);
    public Vector3 start_rot = new Vector3(64.133f, 8.814f, 10.113f);
    public float start_fov = 36f;
    private param start_param;

    public Vector3 RT_resolution = new Vector3(1920, 1080, 32);

    public fitness_method FitMethod = fitness_method.SIFT;

    public bool is_evaluate = true;

    private bool is_gt = false;
    private bool is_routine = false;
    private CamParameters gt_param;
    private List<CamParameters> CamList = new List<CamParameters>();

    private Mat gt_edge;
    private Mat gt_mat;

    private bool is_find_global = false;
    private bool is_find_optimal = false;
    private bool is_start = true;
    public static bool is_pause = false;

    private CamParameters local_minimum;

    public static string Experiment_path = "Assets/CamOptimizer/Test/Runtime/Resources/Experiments/";

    // Update is called once per frame
    void Update()
    {
        //iterate();
    }

    public void iterate()
    {
        if (is_start)
        {
            is_start = false;
            CamParameters.SetRT_resolution(RT_resolution);

            cam = cam_tf.GetComponent<Camera>();
            cam_rt = cam.targetTexture;
            start_param.pose = start_pos;
            start_param.euler_rot = start_rot;
            start_param.fov = start_fov;
        }
        else
        {
            if (!is_gt)
            {
                if (cam.activeTexture == true)
                {
                    is_gt = true;
                    // Make Ground truth
                    gt_mat = Utilities.RenderToMat(cam_rt);
                    gt_edge = gt_mat.Canny(0, 300);
                    gt_image.texture = cam_rt;

                    gt_param = new CamParameters(cam_tf.position, cam_tf.eulerAngles, cam.fieldOfView);
                    gt_param.AssignObjects(cam_tf, cam, cam_rt);

                    InitPopulation();

                }
            }
            else if (!is_routine)
            {
                int i = 0;
                foreach (var c in CamList)
                {
                    if (c.cam.activeTexture == false)
                    {
                        break;
                    }
                    i++;
                }
                if (i == CamList.Count)
                {
                    StartCoroutine(optimize());
                    is_routine = true;
                    //Thread thread = new Thread(optimizer);
                    //thread.Start();
                }
            }

            if (is_find_optimal && !is_find_global)            // find another optimal
            {

                if (local_minimum != null)
                {
                    if (local_minimum.fitness < CamList[CamList.Count - 1].fitness)
                    {
                        local_min_change_num++;
                        local_minimum.DestroyObjects();

                        local_minimum = CamList[CamList.Count - 1];
                        local_minimum.cam_tf.name = "local optimal_episode_" + episode.ToString() + "_changed_" + local_min_change_num.ToString();
                        local_minimum.ri.gameObject.name = "local optimal_episode_" + episode.ToString() + "_changed_" + local_min_change_num.ToString();
                        local_minimum.ri.color = new Color(1, 1, 1, 0);

                        Debug.Log("local minimum changed");
                    }
                    else
                    {
                        CamList[CamList.Count - 1].DestroyObjects();
                    }
                }
                else   // first time
                {
                    local_minimum = CamList[CamList.Count - 1];
                    local_minimum.cam_tf.name = "local optimal_episode_" + episode.ToString() + "_changed_" + local_min_change_num.ToString();
                    local_minimum.ri.gameObject.name = "local optimal_episode_" + episode.ToString() + "_changed_" + local_min_change_num.ToString();
                    local_minimum.ri.color = new Color(1, 1, 1, 0);

                }

                CamList.RemoveAt(CamList.Count - 1);

                param rand_param = Utilities.RandParam(new Vector3(PosRandomRange * 2, PosRandomRange * 2, PosRandomRange * 2), new Vector3(RotRandomRange * 2, RotRandomRange * 2, RotRandomRange * 2), FovRandomRange * 2);
                for (int i = 0; i < num_population; i++)
                {
                    param tmp_rand_param = Utilities.RandParam(-PosRandomRange, PosRandomRange, -RotRandomRange, RotRandomRange, -FovRandomRange, FovRandomRange);

                    if (i == num_population - 1)
                    {
                        // name should be modified
                        var tmp_cam_param = new CamParameters(rand_param.pose + tmp_rand_param.pose + local_minimum.param.pose, rand_param.euler_rot + tmp_rand_param.euler_rot + local_minimum.param.euler_rot, rand_param.fov, "_episode" + episode.ToString() + "_new one");

                        CamList.Add(tmp_cam_param);

                        var tmp_ui = Instantiate(gt_image.gameObject, gt_image.transform.parent, true);
                        tmp_ui.name = "image_episode_" + episode.ToString() + "_new one";
                        tmp_cam_param.ri = tmp_ui.GetComponent<RawImage>();
                        tmp_cam_param.ri.texture = tmp_cam_param.rt;
                    }
                    else
                    {
                        CamList[i].SetParams(rand_param.pose + tmp_rand_param.pose + local_minimum.param.pose, rand_param.euler_rot + tmp_rand_param.euler_rot + local_minimum.param.euler_rot, rand_param.fov + tmp_rand_param.fov + local_minimum.param.fov, true);

                    }
                }

                is_find_optimal = false;
                is_routine = false;
            }

            Resources.UnloadUnusedAssets();
        }
    }

    void InitPopulation()
    {
        CamParameters tmp_cam_param = new CamParameters(start_param.pose, start_param.euler_rot, start_param.fov, "_1");
        CamList.Add(tmp_cam_param);
        var first_ui = Instantiate(gt_image.gameObject, gt_image.transform.parent, true);
        first_ui.name = "image_1";
        tmp_cam_param.ri = first_ui.GetComponent<RawImage>();
        tmp_cam_param.ri.texture = tmp_cam_param.rt;
        for (int i = 1; i < num_population; i++)
        {
            param rand_param = Utilities.RandParam(-PosRandomRange, PosRandomRange, -RotRandomRange, RotRandomRange, -FovRandomRange, FovRandomRange);

            //var tmp_cam_param = new CamParameters(gt_param.param.pose + rand_param.pose, gt_param.param.euler_rot + rand_param.euler_rot, gt_param.param.fov + rand_param.fov, "_" + (i + 1).ToString());
            tmp_cam_param = new CamParameters(start_param.pose + rand_param.pose, start_param.euler_rot + rand_param.euler_rot, start_param.fov + rand_param.fov, "_" + (i + 1).ToString());

            CamList.Add(tmp_cam_param);

            var tmp_ui = Instantiate(gt_image.gameObject, gt_image.transform.parent, true);
            tmp_ui.name = "image_" + (i + 1).ToString();
            tmp_cam_param.ri = tmp_ui.GetComponent<RawImage>();
            tmp_cam_param.ri.texture = tmp_cam_param.rt;
        }
        
    }

    void SortByFitness()
    {
        // Calculate fitness
        List<float> fitnesses = new List<float>();
        foreach (var cam_param in CamList)
        {
            cam_param.fitness = fitness_fn(cam_param, FitMethod);
            fitnesses.Add(cam_param.fitness);
        }

        // Sort
        Utilities.SortWithIndices(fitnesses, CamList);

        string info = iter.ToString() + "/" + num_iteration.ToString() + " iteration";
        info += "\nepisode : " + episode.ToString();
        info += "\nlocal minimum changed num : " + local_min_change_num.ToString();

        info += "\n\nbest : " + CamList[CamList.Count - 1].cam_tf.name.ToString();
        info += "\nbest distance loss : " + CamList[CamList.Count - 1].dist_loss.ToString();
        info += "\nbest match num : " + CamList[CamList.Count - 1].match_num.ToString();
        info += "\nbest fitness : " + CamList[CamList.Count - 1].fitness.ToString();
        info += "\nbest param : position" + CamList[CamList.Count - 1].param.pose.ToString() + ", rotation" + CamList[CamList.Count - 1].param.euler_rot.ToString() + ", FoV(" + CamList[CamList.Count - 1].param.fov.ToString() + ")";

        info += "\n\nworst : " + CamList[0].cam_tf.name.ToString();
        info += "\nworst distance loss : " + CamList[0].dist_loss.ToString();
        info += "\nworst match num : " + CamList[0].match_num.ToString();
        info += "\nworst fitness : " + CamList[0].fitness.ToString();
        info += "\nworst param : position" + CamList[0].param.pose.ToString() + ", rotation" + CamList[0].param.euler_rot.ToString() + ", FoV(" + CamList[0].param.fov.ToString() + ")";
        Debug.Log(info);

        CamList[CamList.Count - 1].ri.color = new Color(1, 1, 1, 0.5f);
        for (int i = 0; i < CamList.Count - 1; i++)
        {
            CamList[i].ri.color = new Color(1, 1, 1, 0);
        }
    }

    float fitness_fn(CamParameters camparam, fitness_method method)
    {
        float fitness = 0;
        switch (method)
        {
            case fitness_method.TemplateMatching:
                Mat source = Utilities.RenderToMat(camparam.rt);
                Mat res = new Mat();
                Cv2.MatchTemplate(gt_mat, source, res, TemplateMatchModes.CCoeffNormed);
                fitness = -(float)res.At<float>(0, 0);       // unity에서 float : 32byte
                break;
            case fitness_method.SIFT:
                fitness = FeatureLoss(camparam);
                break;
        }

        return fitness;
    }

    // add variance of distance as a loss
    float FeatureLoss(CamParameters camparam)
    {
        Mat source = Utilities.RenderToMat(camparam.rt);
        var sift = SIFT.Create();

        KeyPoint[] keypoints1, keypoints2;
        var descriptors1 = new Mat();   //<float>
        var descriptors2 = new Mat();   //<float>
        sift.DetectAndCompute(source, null, out keypoints1, descriptors1);
        sift.DetectAndCompute(gt_mat, null, out keypoints2, descriptors2);

        var Matcher = new BFMatcher(NormTypes.L2, false);

        DMatch[] matches = Matcher.Match(descriptors1, descriptors2);
        camparam.match_num = matches.Length;

        matches = matches.OrderByDescending(x => x.Distance).ToArray();
        if (matches.Length <= 3)
        {
            return float.MinValue;
        }
        //Debug.Log(matches.Length);
        float min_dist = matches[matches.Length - 1].Distance;
        float max_dist = matches[0].Distance;
        float boundary = (max_dist - min_dist) * FeatureFilterRatio + min_dist;
        List<DMatch> correct_matches = new List<DMatch>();
        for (int i = matches.Length - 1; matches[i].Distance < boundary; i--)
        {
            correct_matches.Add(matches[i]);
        }

        List<double> losses = new List<double>();
        foreach (DMatch match in correct_matches)
        {
            var src_pt = keypoints1[match.QueryIdx].Pt;
            var gt_pt = keypoints2[match.TrainIdx].Pt;

            var dist = src_pt.DistanceTo(gt_pt);
            losses.Add(dist);
        }
        camparam.dist_loss = (float)losses.Average();

        if (camparam.dist_loss < 3)
        {
            is_find_global = true;
            is_find_optimal = true;
        }

        return -camparam.dist_loss * (1 - alpha) + camparam.match_num * alpha;
    }

    void CrossOver()
    {
        // Check overfitting
        if (CheckOverfit(OverfitThres))
        {
            Debug.Log("overfit!!");
            is_find_optimal = true;
            return;
        }

        List<param> tmp_params = new List<param>();
        for (int i = 0; i < num_exchange; i++)
        {
            int parent_1 = CamList.RandomIndexByFitness();
            int parent_2 = CamList.RandomIndexByFitness(parent_1);
            //parent_1 = num_population-1;
            //parent_2 = num_population-2;
            //Debug.Log("parent indices : " + parent_1 + ", " + parent_2);

            // expand
            //param rand_param = Utilities.RandParam(-PosExpandRatio+1, PosExpandRatio, -RotExpandRatio + 1, RotExpandRatio, -FovExpandRatio + 1, FovExpandRatio);

            //Vector3 tmp_pos = CamList[parent_1].param.pose + Vector3.Scale(rand_param.pose,(CamList[parent_2].param.pose - CamList[parent_1].param.pose));
            //var tmp_quat = Quaternion.LerpUnclamped(CamList[parent_1].param.quat_rot, CamList[parent_2].param.quat_rot, rand_param.euler_rot.x);
            ////Vector3 tmp_rot = CamList[parent_1].param.euler_rot + Vector3.Scale(rand_param.euler_rot, (CamList[parent_2].param.euler_rot - CamList[parent_1].param.euler_rot));
            //float tmp_fov = CamList[parent_1].param.fov + rand_param.fov * (CamList[parent_2].param.fov - CamList[parent_1].param.fov);

            // interpolation
            param rand_param = Utilities.RandParam(new Vector3(PosExpandRatio, PosExpandRatio, PosExpandRatio), new Vector3(RotExpandRatio, RotExpandRatio, RotExpandRatio), FovExpandRatio);
            Vector3 rand_prf = new Vector3(PosExpandRatio, RotExpandRatio, FovExpandRatio);
            var tmp_pos = CamList[parent_1].param.pose + rand_prf.x * (CamList[parent_2].param.pose - CamList[parent_1].param.pose);
            tmp_pos += rand_param.pose;
            var tmp_quat = Quaternion.LerpUnclamped(CamList[parent_1].param.quat_rot, CamList[parent_2].param.quat_rot, rand_prf.y);
            var tmp_rot = new Vector3(tmp_quat.eulerAngles.x, tmp_quat.eulerAngles.y, tmp_quat.eulerAngles.z);
            tmp_rot += rand_param.euler_rot;
            var tmp_fov = CamList[parent_1].param.fov + rand_prf.z * (CamList[parent_2].param.fov - CamList[parent_1].param.fov);

            var tmp_param = mutation(tmp_pos, tmp_rot, tmp_fov);
            tmp_params.Add(tmp_param);
        }

        for (int i = 0; i < num_exchange; i++)
        {
            CamList[i].SetParams(tmp_params[i], true);
        }
    }

    bool CheckOverfit(float threshold)
    {
        float[] fits = new float[CamList.Count];
        for (int i = 0; i < CamList.Count; i++) fits[i] = CamList[i].fitness;

        float[] vars = new float[fits.Length];
        for (int i = 0; i < fits.Length; i++) vars[i] = Mathf.Pow(fits[i] - fits.Average(), 2);

        if (Mathf.Sqrt(vars.Average()) < threshold) return true;

        return false;
    }

    param mutation(Vector3 pose, Vector3 rotation, float fov)
    {
        param param = new param();
        param.pose = pose;
        param.euler_rot = rotation;
        param.fov = fov;

        var pos_range = PosRandomRange;
        var rot_range = RotRandomRange;
        var fov_range = FovRandomRange;

        float q = UnityEngine.Random.Range(0f, 1f);
        if (q <= child_mutation_prob)
        {
            //if (CamList[CamList.Count - 1].dist_loss < 200)
            //{
            //    pos_range /= 2;
            //    rot_range /= 2;
            //    fov_range /= 2;
            //}
            param rand_param;
            rand_param.pose = new Vector3(0, 0, 0);
            rand_param.euler_rot = new Vector3(0, 0, 0);
            rand_param.fov = 0;
            float p = UnityEngine.Random.Range(0f, 1f);
            if (p < component_mutation_prob)
            {
                rand_param = Utilities.RandParam(new Vector3(pos_range, 0, 0), Vector3.zero, 0);
            }
            p = UnityEngine.Random.Range(0f, 1f);
            if (p < component_mutation_prob)
            {
                rand_param = Utilities.RandParam(new Vector3(0, pos_range, 0), Vector3.zero, 0);
            }
            p = UnityEngine.Random.Range(0f, 1f);
            if (p < component_mutation_prob)
            {
                rand_param = Utilities.RandParam(new Vector3(0, 0, pos_range), Vector3.zero, 0);
            }
            p = UnityEngine.Random.Range(0f, 1f);
            if (p < component_mutation_prob)
            {
                rand_param = Utilities.RandParam(Vector3.zero, new Vector3(rot_range, 0, 0), 0);
            }
            p = UnityEngine.Random.Range(0f, 1f);
            if (p < component_mutation_prob)
            {
                rand_param = Utilities.RandParam(Vector3.zero, new Vector3(0, rot_range, 0), 0);
            }
            p = UnityEngine.Random.Range(0f, 1f);
            if (p < component_mutation_prob)
            {
                rand_param = Utilities.RandParam(Vector3.zero, new Vector3(0, 0, rot_range), 0);
            }
            p = UnityEngine.Random.Range(0f, 1f);
            if (p < component_mutation_prob)
            {
                rand_param = Utilities.RandParam(Vector3.zero, Vector3.zero, fov_range);
            }
            param.pose += rand_param.pose;
            param.euler_rot += rand_param.euler_rot;
            param.fov += rand_param.fov;
        }
        return param;
    }

    IEnumerator optimize()
    {
        episode++;
        while (iter < num_iteration)
        {
            if (!is_pause)
            {
                iter++;
                SortByFitness();
                CrossOver();
                if (is_evaluate)
                {
                    // fitness, loss(Similarity), pos_error, rot_error, step : 5step마다 기록, restart 여부(is_find_optimal)
                    if (iter == 1)
                    {
                        string start_record = "=============Start record=============";
                        start_record += "\nStep, Fitness, Distance loss(-Similarity), position error(Euclidian Distance), rotation error(degree angle)";
                        Record(start_record, true);
                    }
                    if (iter%5  == 1)
                    {
                        string record = "\n" +iter.ToString();
                        record += ", "+CamList[CamList.Count-1].fitness.ToString();
                        record += ", "+CamList[CamList.Count - 1].dist_loss.ToString();
                        record += ", "+(CamList[CamList.Count - 1].cam_tf.position - gt_param.cam_tf.position).magnitude.ToString();
                        record += ", "+(Quaternion.Angle(CamList[CamList.Count - 1].cam_tf.rotation, gt_param.cam_tf.rotation)*180/Mathf.PI).ToString();
                        
                        Record(record);

                        //button.gameObject.SetActive(false);
                        string im_path = Experiment_path +SceneManager.GetActiveScene().name + "/ChangesOfImages/" + string.Format("{0:D4}.png", iter);
                        ScreenCapture.CaptureScreenshot(im_path);
                        //button.gameObject.SetActive(true);
                    }
                    
                }
            }

            if (is_find_optimal)
            {
                string record = "\n=============Find optimal=============";
                Record(record);
                break;
            }
            yield return new WaitForSeconds(1f);
        }
        if (local_minimum != null)
            CamList.Add(local_minimum);

        string info = "finished at " + iter.ToString() + "th iteration, " + episode.ToString() + "th episode with " + local_min_change_num.ToString() + " local minimum change";
        CamList.SaveInformation(info, "Assets/CamOptimizer/Test/Runtime/Resources/Experiments/20240315_cam_optim_result_episode_" + episode.ToString() + ".txt");

        if (local_minimum != null)
            CamList.Remove(local_minimum);

        if (iter > num_iteration)
        {
            is_find_global = true;
        }
    }

    private void Record(string record, bool create = false)
    {
        string txt_path = Experiment_path + SceneManager.GetActiveScene().name + "/Errors/info.txt";
        StreamWriter writer;
        if (create)
        {
            writer = File.CreateText(txt_path);
        }
        else
        {
            writer = File.AppendText(txt_path);
        }
        writer.Write(record);
        writer.Close();
    }

    private void OnDestroy()
    {
        Cv2.DestroyAllWindows();
    }

}
