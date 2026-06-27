using UnityEngine;

public class AntMovement : MonoBehaviour
{
    public float moveSpeed = 5f;        // Ўвидк≥сть б≥гу мурахи
    public Joystick joystick;           // —юди ми перет€гнемо наш джойстик

    private Rigidbody2D rb;
    private Animator anim;
    private Vector2 moveInput;
    private bool facingRight = true;    // „и дивитьс€ мураха вправо на старт≥

    void Start()
    {
        // јвтоматично знаходимо компоненти на сам≥й мурас≥
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
    }

    void Update()
    {
        // ќтримуЇмо дан≥ з в≥ртуального джойстика
        moveInput.x = joystick.Horizontal;
        moveInput.y = joystick.Vertical;

        // ѕерев≥р€Їмо, чи рухаЇтьс€ джойстик (€кщо значенн€ не нульов≥)
        if (moveInput.x != 0 || moveInput.y != 0)
        {
            // ¬микаЇмо ан≥мац≥ю б≥гу (IsRunning = true)
            anim.SetBool("IsRunning", true);
        }
        else
        {
            // якщо джойстик в≥дпущений Ч мураха стоњть (IsRunning = false)
            anim.SetBool("IsRunning", false);
        }

        // јвтоматичний розворот спрайту л≥воруч/праворуч
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
        // ‘≥зичне перем≥щенн€ мурахи в 2D простор≥
        rb.MovePosition(rb.position + moveInput * moveSpeed * Time.fixedDeltaTime);
    }

    // ‘ункц≥€ дл€ дзеркального розвороту мурахи
    void Flip()
    {
        facingRight = !facingRight;
        Vector3 scaler = transform.localScale;
        scaler.x *= -1;
        transform.localScale = scaler;
    }
}