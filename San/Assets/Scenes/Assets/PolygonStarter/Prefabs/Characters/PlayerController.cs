using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField]
    public float moveSpeed;

    public Transform cameraTransform;
    private Rigidbody rb;
    // Start is called before the first frame update
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        //读取输入

        Vector3 camForward = cameraTransform.forward;
        Vector3 camRight = cameraTransform.right;
        //计算摄像机的方向

        camForward.y = 0;
        camRight.y = 0;

        Vector3 moveDir = (camForward.normalized * v + camRight.normalized * h).normalized;
        //计算移动方向

        if (moveDir.sqrMagnitude > 0.001f)
        {
            Ray ray =new Ray(transform.position + Vector3.up * 0.5f, moveDir);
            float rayDistance = 0.6f;

            if(Physics.Raycast(ray,out RaycastHit hitinfo, rayDistance))
            {
                Debug.Log("前方有障碍物：" + hitinfo.collider.name);
                Debug.DrawRay(ray.origin, moveDir * rayDistance, Color.red, 0.2f);
            }
            else {
                Debug.DrawRay(ray.origin, moveDir * rayDistance, Color.green, 0.2f);
            }

            //角色移动
            Vector3 velocity = moveDir * moveSpeed;
            velocity.y = rb.velocity.y;
            rb.velocity = velocity;

            //角色朝向移动方向
            transform.rotation = Quaternion.LookRotation(moveDir, Vector3.up);
        }

        else
        {
            Vector3 velocity = new Vector3(0, rb.velocity.y, 0);
            rb.velocity = velocity;
        }
    }
}
