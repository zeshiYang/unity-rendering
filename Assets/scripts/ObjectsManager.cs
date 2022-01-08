using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;


[System.Serializable]
class SceneMotion
    //the class of the scene motion
{
    public int num_objects;// the number of objects to be manipulated
    public float[] motions;// the motions of the object
    public int[] geom_types;// types of the geoms 0: sphere, 1: box, 2: capsule
    public float[] shapes;
    public float[] chop1_motions;// the motion of the chop1
    public float[] chop2_motions;//the motion of the chop2
    public float[] hand_motions;//the motion of the hand
    public int[] idx_start;
    public int[] idx_end;
    public float[] contact_force;

}

public class ObjectMotionGenerator
{
    public float[] motion;
    public float motion_time = 0.0f;
    public float dt = 0.01f;//delta time between two frames
    public ObjectMotionGenerator(float[] data)
    {
        this.motion = data;
        this.motion_time = this.dt * (motion.Length / 7 - 1);
    }
    public (Vector3, Quaternion) generate(float t)
    {
        //compute the motion of the object
        t = t % this.motion_time;
        int t1 = (int)(t / this.dt);
        int t2 = t1 + 1;
        //Debug.Log(this.motion.Length);
        float alpha = (t2 * this.dt - t) / this.dt;
        float[] pos1 = this.motion.Skip(t1 * 7).Take(t1 * 7 + 3).ToArray();
        float[] pos2 = this.motion.Skip(t2 * 7).Take(t2 * 7 + 3).ToArray();

        float[] quat1 = this.motion.Skip(t1 * 7 + 3).Take(t1 * 7 + 7).ToArray();
        float[] quat2 = this.motion.Skip(t2 * 7 + 3).Take(t2 * 7 + 7).ToArray();

        Vector3 pos_start = new Vector3(pos1[0], pos1[1], pos1[2]);
        Vector3 pos_end = new Vector3(pos2[0], pos2[1], pos2[2]);

        Quaternion quat_start = new Quaternion(quat1[1], quat1[2], quat1[3], quat1[0]);
        Quaternion quat_end = new Quaternion(quat2[1], quat2[2], quat2[3], quat2[0]);

        Vector3 pos = alpha * pos_start + (1 - alpha) * pos_end;
        Quaternion quat = Quaternion.Slerp(quat_start, quat_end, alpha);

        return (pos, quat);
    }
}

public class ObjectsManager : MonoBehaviour
{
    // Start is called before the first frame update
    private List<GameObject> objects_list = new List<GameObject>();
    private List<ObjectMotionGenerator> object_motion_generator_list = new List<ObjectMotionGenerator>();//motion generator for all objects
    private List<ObjectMotionGenerator> hand_geom_generator_list = new List<ObjectMotionGenerator>();//motion generator for all hand geoms
    public Material[] material_prefab;
    public GameObject[] chops;
    public GameObject hand;
    public TextAsset text_asset;
    public bool render_contact;
    private float t = 0;
    private List<GameObject> contact_force = new List<GameObject>();
    private int[] idx_start;
    private int[] idx_end;
    private float[] force;
    private float motion_time;

    private string[] hand_geoms = {"upperarm", "lowerarm","palm", "ff_cmc", "ffproximal", "ffmiddle", "ffdistal", "mf_cmc", "mfproximal", "mfmiddle", "mfdistal" 
    , "rf_cmc", "rfproximal", "rfmiddle", "rfdistal", "thproximal", "thmiddle", "thdistal", "lf_cmc", "lfproximal", "lfmiddle", "lfdistal"};
    void Start()
    {

        //Debug.Log(text_asset);
        SceneMotion scene_motion = new SceneMotion();//obj2里面是混乱的初始值
        JsonUtility.FromJsonOverwrite(text_asset.text, scene_motion);//把json_string字符串解析到obj2中
        this.motion_time = (float)(0.01 * (scene_motion.chop1_motions.Length / 7.0f - 1));
        this.force = scene_motion.contact_force.Skip(0).Take(scene_motion.contact_force.Length).ToArray();
        this.idx_start = scene_motion.idx_start.Skip(0).Take(scene_motion.idx_start.Length).ToArray();
        this.idx_end = scene_motion.idx_end.Skip(0).Take(scene_motion.idx_start.Length).ToArray();
        Debug.Log(this.idx_start[0]);
        // load motions of hand geoms
        for (int i = 0; i<hand_geoms.Length; i++)
        {
            int motion_frames = scene_motion.hand_motions.Length / hand_geoms.Length;
            float[] motion_names = scene_motion.hand_motions.Skip(i * motion_frames).Take(motion_frames).ToArray();

            ObjectMotionGenerator hand_motion_generator = new ObjectMotionGenerator(motion_names);
            hand_geom_generator_list.Add(hand_motion_generator);

        }

        for(int i=0; i< scene_motion.num_objects;i++)
        {
            //Debug.Log(scene_motion.geom_types[i]);
            int geom_type = scene_motion.geom_types[i];
            float[] shape = { scene_motion.shapes[3 * i], scene_motion.shapes[3 * i + 1], scene_motion.shapes[3 * i + 2] };
            int motion_frames = scene_motion.motions.Length / scene_motion.num_objects;
            //float[] motion = new ArraySegment<float>(scene_motion.motions,  i * motion_frames, (i+1) * motion_frames).Array;
            float[] motion = scene_motion.motions.Skip(i * motion_frames).Take(motion_frames).ToArray();
           
            ObjectMotionGenerator object_motion_generator = new ObjectMotionGenerator(motion);
            object_motion_generator_list.Add(object_motion_generator);
            if(geom_type == 0)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.localScale = new Vector3( 2 * shape[0],  2 * shape[0],  2 * shape[0]);
                Renderer rend = sphere.GetComponent<Renderer>();
                rend.material = material_prefab[UnityEngine.Random.Range(0, material_prefab.Length)];
                objects_list.Add(sphere);
            }
            else if(geom_type ==1)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.localScale = new Vector3(2 * shape[0], 2 * shape[2], 2 * shape[1]);
                Renderer rend = cube.GetComponent<Renderer>();
                rend.material = material_prefab[UnityEngine.Random.Range(0, material_prefab.Length)];
                objects_list.Add(cube);

            }
            else if(geom_type == 2)
            {
                GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.transform.localScale = new Vector3(2 * shape[0], shape[2] + shape[0], 2 * shape[0]);
                Renderer rend = capsule.GetComponent<Renderer>();
                rend.material = material_prefab[UnityEngine.Random.Range(0, material_prefab.Length)];
                objects_list.Add(capsule);
            }
            else
            {
            }
        }
  
        float[] motion1 = scene_motion.chop1_motions;

        float[] motion2 = scene_motion.chop2_motions;

        ObjectMotionGenerator object_motion_generator_chop1 = new ObjectMotionGenerator(motion1);
        object_motion_generator_list.Add(object_motion_generator_chop1);

        ObjectMotionGenerator object_motion_generator_chop2 = new ObjectMotionGenerator(motion2);
        object_motion_generator_list.Add(object_motion_generator_chop2);


        //for(int i = 0; i<10; i++)
        //{
        //    GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        //    cube.transform.position = new Vector3(0, 0.2f + i * 0.02f, 0);
        //    cube.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        //    Renderer rend = cube.GetComponent<Renderer>();
        //    rend.material = material_prefab[Random.Range(0, material_prefab.Length)];
        //    objects_list.Add(cube);
        //}
    }

    // Update is called once per frame
    void Update()
    {
        this.DrawObjects();
        this.DrawHand();
        this.DrawContact();
        t += Time.deltaTime * 0.6f;
    }

    void DrawObjects()
    {
        for (int i = 0; i < this.object_motion_generator_list.Count - 2; i++)
        {
            (Vector3 pos, Quaternion quat) = this.object_motion_generator_list[i].generate(t);
            this.objects_list[i].transform.position = pos;
            this.objects_list[i].transform.rotation = quat;
        }

        (Vector3 pos_chop1, Quaternion quat_chop1) = this.object_motion_generator_list[this.object_motion_generator_list.Count - 2].generate(t);
        (Vector3 pos_chop2, Quaternion quat_chop2) = this.object_motion_generator_list[this.object_motion_generator_list.Count - 1].generate(t);
        chops[0].transform.position = pos_chop1;
        //Debug.Log(pos_chop1);
        chops[0].transform.rotation = quat_chop1;

        chops[1].transform.position = pos_chop2;
        chops[1].transform.rotation = quat_chop2;

        //Debug.Log(chops[0].name);
      
    }

    void DrawHand()
    {
        GameObject hand_geom;
        //hingeJoints = GetComponentsInChildren(typeof(HingeJoint));
        for (int i= 0; i< this.hand_geoms.Length;i++)
        {
           
            string name = this.hand_geoms[i] + "_geom";
            hand_geom = GameObject.Find(name);
            //Debug.Log(hand_geom.name);
            (Vector3 pos, Quaternion quat) = this.hand_geom_generator_list[i].generate(t);
            hand_geom.transform.position = pos;
            hand_geom.transform.rotation = quat;
        }

    }

    void DrawContact()
    {
        if(this.render_contact)
        {
           
            if(this.contact_force!=null)
            {
                for(int i= 0; i< this.contact_force.Count;i++)
                {
                    Destroy(this.contact_force[i]);
                }
                contact_force.Clear();
            }

            float t_current = t % this.motion_time;
            int t1 = (int)(t_current / 0.01f);
            int start_idx = this.idx_start[t1];
            int end_idx = this.idx_end[t1];
            Debug.Log(start_idx);
            Debug.Log(end_idx);
            int num_contact_forces = (end_idx - start_idx) / 10;
            for(int i= 0;i<num_contact_forces;i++)
            {
                GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                float [] force_info = this.force.Skip(start_idx + i * 10).Take(10).ToArray();
                Vector3 force_pos = new Vector3(force_info[0], force_info[1], force_info[2]);
                Vector3 force_value = new Vector3(force_info[3], force_info[4], force_info[5]);
                Quaternion q = new Quaternion(force_info[7], force_info[8], force_info[9], force_info[6]);
                capsule.transform.localScale = new Vector3(0.002f, force_value.magnitude * 0.1f, 0.002f);
                capsule.transform.rotation = q;
                capsule.transform.position = force_pos;
                Renderer rend = capsule.GetComponent<Renderer>();
                rend.material = material_prefab[3];
                this.contact_force.Add(capsule);                
            }
        }
    }
}
