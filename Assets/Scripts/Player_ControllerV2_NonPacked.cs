using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//[RequireComponent(typeof(BoxCollider))]
//[RequireComponent(typeof(LayerMask))]
public class Player_ControllerV2 : MonoBehaviour
{
    [SerializeField] private float movementSpeed; ///Literal Movment Speed Variable
    [SerializeField] private float mouseSensitivity; ///Literal mouseSensitivity * other
    [SerializeField] private float jumpForce; ///Literal jumpForce Variable
    [SerializeField] private BoxCollider ground; //ground detection collider Variable
    [SerializeField] private LayerMask groundMask; ///Layermask for groundmask
    private Rigidbody rb; //Local function
    private Vector2 movement = Vector2.zero; //wasd
           
    private bool grounded = false; // local bool for grounded
    /// <summary>
    //Awaken the boxCollider
    /// </summary>
        //private void Awake()
   // {
        // Ensure the BoxCollider is set as a trigger SET TO GROUND
      //  BoxCollider boxCollider = GetComponent<BoxCollider>();
      //  boxCollider.isTrigger = true;
   // }
   private void Start()
    {
  
       rb = GetComponent<Rigidbody>(); //rb
    

    }

    // Update is called once per frame
    void Update()
    {
        ///WASD VECTORS  *  movement  
      movement = new Vector2(Input.GetAxisRaw("Horizontal"),  Input.GetAxisRaw("Vertical"));
     //mouse movement
     Vector2 mouse = mouseSensitivity * Time.deltaTime * new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
//rotate character
    transform.Rotate(mouse.x * Vector3.up);
    //check ground + jump/space = new force
    if (grounded && Input.GetKeyDown(KeyCode.Space))
        rb.AddForce(jumpForce * Vector3.up, ForceMode.Impulse); //Add upwards force
    }

    private void FixedUpdate()    
    {
        //getvelocity dir
        Vector3 velocity = movementSpeed * (movement.x * transform.right + movement.y * transform.forward);
        //velocity
        rb.velocity = new Vector3(velocity.x, rb.velocity.y, velocity.z);
        //update grounded
        grounded = Physics.CheckBox(ground.transform.position + ground.center, 0.5f * ground.size, ground.transform.rotation, groundMask);
   
    }

}

