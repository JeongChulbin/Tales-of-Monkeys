using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UI;
using Cinemachine;
public class playerMove : MonoBehaviourPunCallbacks, IPunObservable
{
    public bool             isLadder = false; // 사다리
    public bool             isRope = false; // 로프
    public float            MaxSpeed; // 최고속도
    public float            LadderSpeed;
    public float            JumpPower; // 점프파워
    public float            Power;
    public int              jumpCount = 2; // 점프 할 수 있는 횟수

    public Rigidbody2D      rigid; // RigidBody 사용

    private PlayerSensor    groundSensor;
    private PlayerSensor    wallSensor1;
    private PlayerSensor    wallSensor2;
    private bool            nowRope;
    private bool            isDash;
    private bool            isMove;
    private bool            isGrounded = false;
    private bool            isDead = false;
    private bool            wallSensor = false;
    private int             facingDir;
    private bool            isLever;

    FixedJoint2D            fixjoint;
    Rigidbody2D             ropeRigid;
    Animator                anim; // Animator 사용
    SpriteRenderer          render;
    public Sprite           MonkeySprite;

    // 포톤
    float                   distance;
    public PhotonView       view;
    public Image            NickName;
    public GameObject[]     transform_distance = new GameObject[2];
    Vector3                 notIsMinePosition;
    public CinemachineVirtualCamera CM;

    void Awake()
    {
        fixjoint = GetComponent<FixedJoint2D>(); // 로프 고정을 위함
        rigid = GetComponent<Rigidbody2D>(); // rigidbody 컴포넌트 호출
        anim = GetComponent<Animator>(); // Animator 컴포넌트 호출
        view = GetComponent<PhotonView>();
        render = (SpriteRenderer)gameObject.GetComponent<SpriteRenderer>();
        render.sprite = MonkeySprite;

        groundSensor = transform.Find("GroundSensor").GetComponent<PlayerSensor>();
        wallSensor1 = transform.Find("WallSensor1").GetComponent<PlayerSensor>();
        wallSensor2 = transform.Find("WallSensor2").GetComponent<PlayerSensor>();

        NickName.color = view.IsMine ? Color.green : Color.red;

        if (view.IsMine)
        {
            CM = GameObject.Find("CMCamera").GetComponent<CinemachineVirtualCamera>();
            CM.Follow = transform;
            CM.LookAt = transform;
            CM.m_Lens.OrthographicSize = 3f;
        }
        view.RPC("OnTarget", RpcTarget.AllBuffered);

        // 줄 고정 초기화
        fixjoint.connectedBody = null;
        fixjoint.enabled = false;
    }

    void Update()
    {
        if (photonView.IsMine)
        {
            // 캐릭터가 방금 땅에 닿았는지 확인
            if (!isGrounded && groundSensor.State())
            {
                isGrounded = true;
                // //m_animator.SetBool("Grounded", m_grounded);
            }
            if (isGrounded && !groundSensor.State())
            {
                isGrounded = false;
                //m_animator.SetBool("Grounded", m_grounded);
            }

            float InputX = Input.GetAxisRaw("Horizontal");
            float InputY = Input.GetAxisRaw("Vertical");

            //Jump
            if (Input.GetButtonDown("Jump") && jumpCount >= 0) //2단 점프 가능
            {
                if (jumpCount > 0)
                {
                    jumpCount--;
                    if (jumpCount > 2) return;

                    anim.SetBool("isJumping", true);

                    if (fixjoint.connectedBody != null)
                    {
                        fixjoint.connectedBody = null;
                        fixjoint.enabled = false;
                        nowRope = false;
                    }
                    Jump();
                }
            }
            else if (Input.GetKeyDown("e") && !photonView.Owner.IsMasterClient)
            {
                if (isRope)
                {
                    fixjoint.connectedBody = ropeRigid;
                    fixjoint.enabled = true;
                    nowRope = true;
                }
            }
            else if (Input.GetKeyDown(KeyCode.LeftShift))
            {
                isDash = true;
                OnDash(InputX);
            }
            else if (Input.GetKeyDown("e") && isLever)
            {
                Debug.Log("레버 작동");
            }
            if (isGrounded && rigid.velocity.y == 0) //착지를 체크하여 점프애니메이션 종료
            {
                jumpCount = 2;
                anim.SetBool("isJumping", false);
            }

            //Animation
            if (InputX != 0)       
            {
                isMove = true;
                if(rigid.velocity.y == 0)
                {
                    anim.SetBool("isWalking", true);
                    anim.SetBool("isJumping", false);
                }
                else
                {
                    anim.SetBool("isWalking", false);
                    anim.SetBool("isJumping", true);
                }
                view.RPC("Flip", RpcTarget.AllBuffered, InputX); //방향전환
                if (InputX < 0)
                    facingDir = -1;
                else if (InputX > 0)
                    facingDir = 1;
            }
            else if (InputX == 0)
            {
                isMove = false;
                anim.SetBool("isWalking", false);
            }
        }
        else if ((transform.position - notIsMinePosition).sqrMagnitude >= 100)
        {
            transform.position = notIsMinePosition;
        }
        else
        {
            transform.position = Vector3.Lerp(transform.position, notIsMinePosition, Time.deltaTime * 10);
        }
    }

    void FixedUpdate()
    {
        if (photonView.IsMine)
        {
            //Move Speed
            float InputX = Input.GetAxis("Horizontal");
            float InputY = Input.GetAxis("Vertical");

            float SlowDownSpeed = isMove ? 1.0f : 0.5f;
            rigid.velocity = new Vector2(InputX * MaxSpeed * SlowDownSpeed, rigid.velocity.y);

            if (isRope)
            {
                rigid.AddForce(new Vector2(InputY * MaxSpeed, 0));
            }

           else if (isLadder && Mathf.Abs(InputY) > Mathf.Epsilon && InputY != 0)
            {
                rigid.gravityScale = 0;
                rigid.velocity = new Vector2(rigid.velocity.x * 0.5f, LadderSpeed * InputY);
                anim.SetBool("isClimbing", true);
                anim.SetBool("isWalking", false);
                anim.SetBool("isJumping", false);
                anim.SetBool("isStaying", false);
            }
            else
            {
                anim.SetBool("isStaying", true);
                anim.SetBool("isClimbing", false);
                anim.SetBool("isWalking", true);
              
            }
             if (!isLadder)
            {
                anim.SetBool("isStaying", false);
            }

            if (transform_distance.Length == 2)
            {
                distance = Vector2.Distance(transform_distance[0].transform.position, transform_distance[1].transform.position);
                CM.m_Lens.OrthographicSize = Mathf.Lerp(CM.m_Lens.OrthographicSize, distance, Time.deltaTime);
                if (distance <= 4)
                {
                    CM.m_Lens.OrthographicSize = 4f;
                }else if(distance > 6)
                {
                    CM.m_Lens.OrthographicSize = 6f;
                }
            }
            if(wallSensor1 && wallSensor2)
            {
                // 밀기 애니메이션 추가
                wallSensor = wallSensor1.State() && wallSensor2.State();
                if (wallSensor && InputX == 1)
                {
                    Debug.Log("오른벽");
                }
                else if (wallSensor && InputX == -1)
                {
                    Debug.Log("왼벽");
                }
            }

            if (!isLadder)
                rigid.gravityScale = 1f;
        }

    }

    private void OnDash(float Hor)
    {
        if (isDash && fixjoint.connectedBody == null)
        {
            //rigid.velocity = new Vector2(MaxSpeed * Power * InputX, rigid.velocity.y);
            transform.position = new Vector2(transform.position.x + Hor, transform.position.y);
            isDash = false;
        }
    }

    private void Jump()
    {
        rigid.velocity = new Vector2(rigid.velocity.x, JumpPower);
    }

    [PunRPC]
    void OnTarget()
    {
        transform_distance = GameObject.FindGameObjectsWithTag("Player");
    }

    [PunRPC]
    void Flip(float inputX)
    {
        transform.localScale = new Vector3(inputX/2, 0.5f, 0.5f);
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.tag == "Enemy")
            OnDamaged(collision.transform.position);
    }
    // 사다리, 밧줄
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Ladder"))
            isLadder = true;
        if (other.CompareTag("Rope"))
        {
            ropeRigid = other.gameObject.GetComponent<Rigidbody2D>();
            isRope = true;
            other.GetComponent<Rigidbody2D>().IsAwake();
        }
        if (other.CompareTag("Lever"))
        {
            isLever = true;
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Ladder"))
            isLadder = false;
        if (other.CompareTag("Rope"))
        {
            isRope = false;
            ropeRigid = null;
            other.GetComponent<Rigidbody2D>().Sleep();
        }
        if (other.CompareTag("Lever"))
        {
            isLever = false;
        }
    }

    void OnDamaged(Vector2 targetpos)
    {
        gameObject.layer = 11;

        //spriteRenderer.color = new Color(1, 1, 1, 0.4f);
        int dirc = transform.position.x - targetpos.x > 0 ? 1 : -1;
        rigid.AddForce(new Vector2(dirc,1)*7, ForceMode2D.Impulse);

        Invoke("OffDamaged", 3);
    }

    void OffDamaged()
    {
        gameObject.layer = 10;
        //spriteRenderer.color = new Color(1, 1, 1, 1);
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(transform.position);
        }
        else
        {
            notIsMinePosition = (Vector3)stream.ReceiveNext();
        }
    }
}
