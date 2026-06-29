using UnityEngine;

public class AntMovement : MonoBehaviour
{
    public float moveSpeed = 5f;        // �������� ��� ������
    public Joystick joystick;           // ���� �� ����������� ��� ��������

    private Rigidbody2D rb;
    private Animator anim;
    private Vector2 moveInput;
    private bool facingRight = true;    // �� �������� ������ ������ �� �����

    void Start()
    {
        // ����������� ��������� ���������� �� ���� �����
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        // �������� ���� � ����������� ���������
        moveInput.x = joystick.Horizontal;
        moveInput.y = joystick.Vertical;

        // ����������, �� �������� �������� (���� �������� �� ������)
        if (moveInput.x != 0 || moveInput.y != 0)
        {
            // ������� �������� ��� (IsRunning = true)
            anim.SetBool("IsRunning", true);
        }
        else
        {
            // ���� �������� ��������� � ������ ����� (IsRunning = false)
            anim.SetBool("IsRunning", false);
        }

        // ������������ �������� ������� ������/��������
        if (moveInput.x > 0 && !facingRight)
        {
            Flip();
        }
        else if (moveInput.x < 0 && facingRight)
        {
            Flip();
        }
    }

    void FixedUpdate()
    {
        // Գ����� ���������� ������ � 2D �������
        rb.MovePosition(rb.position + moveInput * moveSpeed * Time.fixedDeltaTime);
    }

    // ������� ��� ������������ ��������� ������
    void Flip()
    {
        facingRight = !facingRight;
        Vector3 scaler = transform.localScale;
        scaler.x *= -1;
        transform.localScale = scaler;
    }
}