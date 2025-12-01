using UnityEngine;
[RequireComponent(typeof(CharacterController))]
public class ThirdPersonBasic : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 6f;
    public float rotationSpeed = 10f;
    public float jumpHeight = 2f;
    public float gravity = -40f;   // Stronger gravity for snappier feel
    public float slopeLimit = 45f; // Max walkable slope angle

    [Header("Ground Detection")]
    public Transform groundCheck;
    public float groundDistance = 0.2f;
    public LayerMask groundMask;
    public bool debugGroundCheck = true; // toggle debug drawing/logging

    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;

    // NEW: Jump cooldown to prevent spamming
    private float jumpCooldown = 0.1f;  // short buffer
    private float lastJumpTime = -1f;
    public bool isCrouching;
    Transform camera;
    float slopeLimitCos;
    public static ThirdPersonBasic Instance ;
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(this);
    }
    private void Start()
    {
      position= transform.position;
      slopeLimitCos = Mathf.Cos(slopeLimit * Mathf.Deg2Rad);
      camera =Camera.main.transform;
        controller = GetComponent<CharacterController>();

    }
    Vector3 position;

    private void FixedUpdate()
    {
      if(transform.hasChanged)
        position = transform.position;
      HandleGroundCheck();
      HandleMovement();
      HandleJumpAndGravity();   
        
         
    
        // Example: toggle crouch with Ctrl key
        if (Input.GetKeyDown(KeyCode.LeftControl))
            isCrouching = !isCrouching;
    
    }


private void HandleGroundCheck()
{
    // Raycast straight down for better detection
    isGrounded = Physics.Raycast(position, Vector3.down, controller.height * 0.5f + 0.1f, groundMask);
        // ✅ Debugging info
    //     if (debugGroundCheck)
    //     {
    //         Debug.Log($"[GroundCheck] Grounded: {isGrounded}");
    //     }
    if (isGrounded && velocity.y < 0)
        velocity.y = -2f;
}

    

    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (inputDirection.sqrMagnitude < 0.01f || 
            Physics.Raycast(position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 1.5f, groundMask) 
            && Vector3.Dot(hit.normal, Vector3.up) < slopeLimitCos) return;
            
        // Rotate toward movement direction relative to camera
        float targetAngle = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg + camera.eulerAngles.y;
        Quaternion targetRotation = Quaternion.Euler(0f, targetAngle, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);

        // Move in rotated direction
        Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

        // if (CanWalkOnSlope(moveDirection))
          controller.Move(moveDirection.normalized * moveSpeed * Time.deltaTime);

    }
    private void HandleJumpAndGravity()
    {
      //Would moving this to FixedUpdate Reduce Time.deltaTime calls?
        //  Only allow jump if grounded + cooldown passed
        bool canJumpNow = isGrounded && (Time.deltaTime > /*lastJumpTime +*/ jumpCooldown);

        if (canJumpNow && Input.GetButtonDown("Jump"))
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            // lastJumpTime = Time.time; // prevent spam
            Debug.Log("[Jump] Jumped!");
        }

        // Apply gravity
        velocity.y += gravity * Time.deltaTime;

        // Apply vertical movement
        controller.Move(velocity * Time.deltaTime);
    }
 




    // private bool CanWalkOnSlope(Vector3 moveDirection)=>
    //      !(Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 1.5f, groundMask) && Vector3.Angle(hit?.normal??Vector3.down, Vector3.up)> slopeLimit);
    // {
        // Raycast down to detect slope angle
      // if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit, 1.5f, groundMask))
      //   {
      //       float slopeAngle = Vector3.Angle(hit.normal, Vector3.up);
      //
      //       // if (debugGroundCheck)
      //       // {
      //       //     Debug.Log($"[SlopeCheck] Angle: {slopeAngle}");
      //       // }
      //
      //       return slopeAngle <= slopeLimit;
      //   }
      //   return true; // No ground detected → assume walkable
    // }

    //  Draw Gizmos in Scene View for easier debugging
private void OnDrawGizmosSelected()
{
    if (groundCheck != null)
    {
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
    }
}

}
