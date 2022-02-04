using System.Text;
using UnityEngine;

namespace Interhaptics.HapticRenderer.Core
{
    public class HapticObject : MonoBehaviour
    {
        #region Variables
        public bool RenderStiffness = true;
        public bool RenderTexture = true;
        public bool RenderVibration = true;
        //Encrypted haptic material
        [SerializeField]
        private TextAsset m_material;
        [SerializeField]
        private Vector2 anchorLocalPosition;

        private Vector3 gizmo;
        private Vector3 gizmo2;
        private Collider _collider = null;

        //Unique ID of the object
        [SerializeField]
        private int m_id = -1;
        public int Id { get => m_id; }
        #endregion

        #region Getter setters
        public TextAsset Material
        {
            get => m_material;
            set => UpdateMaterial(value);
        }
        #endregion

        #region Debug
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(gizmo, 0.01f);
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(gizmo2, 0.01f);
        }
        #endregion

        #region Life Cycle
        private void OnValidate()
        {
            anchorLocalPosition.x = Mathf.Clamp(anchorLocalPosition.x, -1, 1);
            anchorLocalPosition.y = Mathf.Clamp(anchorLocalPosition.y, -1, 1);
        }

        private void Awake()
        {
            _collider = gameObject.GetComponent<Collider>();

            m_id = HARWrapper.AddHM(m_material);
        }
        #endregion

        #region Publics
        public bool UpdateMaterial(TextAsset _material)
        {
            if (_material == null)
                return false;
            //if the object does not exist (mainly for haptic objects creation through scripts)
            if (m_id == -1)
            {
                m_id = HARWrapper.AddHM(_material);
                return true;
            }

            //if the object already exists
            bool API_return = HARWrapper.UpdateHM(m_id, _material);

            if (API_return == true)
            {
                m_material = _material;
                return true;
            }
            else
                return false;
        }
        #endregion

        #region Privates
        private void SendCollision(Collider other, HapticBodyPart _hbp)
        {
            float texture_distance = 0;
            float stiffness_distance = 0;

            //Security check
            if (!_collider)
                return;

            if (RenderStiffness || RenderTexture)
            {
                //Collider my_collider = GetComponent<Collider>();

                Vector3 contact_point = transform.InverseTransformPoint(other.transform.TransformPoint(((SphereCollider)other).center));
                SphereCollider my_sphere = null;
                BoxCollider my_box = null;

                if (_collider.GetType() == typeof(SphereCollider))
                {
                    my_sphere = _collider as SphereCollider;
                }

                if (_collider.GetType() == typeof(BoxCollider))
                {
                    my_box = _collider as BoxCollider;

                }

                //texture distance computation if sphere
                if (my_sphere != null)
                {

                    if (RenderStiffness)
                    {
                        stiffness_distance = my_sphere.radius - Vector3.Distance(contact_point, my_sphere.center);
                    }

                    if (RenderTexture)
                    {
                        //Get angle from top of the sphere
                        //Convert to radian
                        //Multiply by radius
                        texture_distance = Vector3.Angle(
                                                   Vector3.Normalize(new Vector3(my_sphere.center.x, my_sphere.center.y + my_sphere.radius, my_sphere.center.z)),
                                                   Vector3.Normalize(contact_point - my_sphere.center)
                                                   ) * Mathf.Deg2Rad
                                                     * my_sphere.radius;
                    }
                }
                else if (my_box != null)
                {
                    //texture computation if cube
                    Vector3 texture_origin = Vector3.zero;
                    Vector3 surface_point = new Vector3();

                    gizmo = transform.TransformPoint(my_box.center) ;

                    //TODO CHECK
                    if (my_box.size.x == 0 || my_box.size.y == 0 || my_box.size.z == 0)
                        return;

                    float dist_x, dist_y, dist_z;
                    dist_x = (contact_point.x - my_box.center.x) / (my_box.size.x/2);
                    dist_y = (contact_point.y - my_box.center.y) / (my_box.size.y/2);
                    dist_z = (contact_point.z - my_box.center.z) / (my_box.size.z/2);

                    if (Mathf.Abs(dist_x) >= Mathf.Abs(dist_y) &&
                        Mathf.Abs(dist_x) >= Mathf.Abs(dist_z))//normal = X axis
                    {
                        if (RenderStiffness)
                        {
                            stiffness_distance = 1 - (Vector3.Distance(contact_point, my_box.center) / (my_box.size.x / 2) * transform.lossyScale.x);
                        }
                        if (RenderTexture)
                        {
                            Vector3 anchor = new Vector3(0, (anchorLocalPosition.x * my_box.size.y / 2), (anchorLocalPosition.y * my_box.size.z / 2));
                            surface_point = new Vector3(my_box.center.x + Mathf.RoundToInt(Mathf.Sign(dist_x)) * my_box.size.x / 2, contact_point.y, contact_point.z);
                            texture_origin = new Vector3(surface_point.x, my_box.center.y, my_box.center.z) + anchor;

                            gizmo = transform.TransformPoint(texture_origin);
                            gizmo2 = transform.TransformPoint(surface_point);
                        }
                    }
                    else if (Mathf.Abs(dist_y) >= Mathf.Abs(dist_z))//normal = Y axis
                    {
                        if (RenderStiffness)
                        {
                            stiffness_distance = 1 - (Vector3.Distance(contact_point, my_box.center) / (my_box.size.y / 2) * transform.lossyScale.y);
                        }

                        if (RenderTexture)
                        {
                            Vector3 anchor = new Vector3((anchorLocalPosition.x * my_box.size.x / 2), 0, (anchorLocalPosition.y * my_box.size.z / 2));
                            surface_point = new Vector3(contact_point.x, my_box.center.y + Mathf.RoundToInt(Mathf.Sign(dist_y)) * my_box.size.y / 2, contact_point.z);
                            texture_origin = new Vector3(my_box.center.x, surface_point.y, my_box.center.z) + anchor;

                            gizmo = transform.TransformPoint(texture_origin);
                            gizmo2 = transform.TransformPoint(surface_point);
                        }
                    }
                    else//normal = Z
                    {
                        if (RenderStiffness)
                        {
                            stiffness_distance = 1 - ((Vector3.Distance(contact_point, my_box.center) / (my_box.size.z / 2)) * transform.lossyScale.z);
                        }

                        if (RenderTexture)
                        {
                            Vector3 anchor = new Vector3((anchorLocalPosition.x * my_box.size.x / 2), (anchorLocalPosition.y * my_box.size.y / 2), 0);
                            surface_point = new Vector3(contact_point.x, contact_point.y, my_box.center.z + Mathf.RoundToInt(Mathf.Sign(dist_z)) * my_box.size.z / 2);
                            texture_origin = new Vector3(my_box.center.x, my_box.center.y, surface_point.z) + anchor;

                            gizmo = transform.TransformPoint(texture_origin);
                            gizmo2 = transform.TransformPoint(surface_point);
                        }
                    }

                    if (RenderTexture)
                        texture_distance = Vector3.Distance(transform.TransformPoint(texture_origin), transform.TransformPoint(surface_point));
                }
            }

            HARWrapper.ComputeHaptics(  m_id, (int)_hbp.BodyPart,
                                        new Vector3(texture_distance, stiffness_distance, 0),
                                        RenderTexture, RenderStiffness, RenderVibration);
        }

        private void OnTriggerEnter(Collider other)
        {
            //TODO MANAGE MULTIPLE COLLIDERS
            HapticBodyPart hbp;
            if ((hbp = other.gameObject.GetComponent<HapticBodyPart>()) != null)
            {
                SendCollision(other, hbp);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            HapticBodyPart hbp;
            if ((hbp = other.gameObject.GetComponent<HapticBodyPart>()) != null)
            {
                SendCollision(other, hbp);
            }
        }
        #endregion
    }
}
