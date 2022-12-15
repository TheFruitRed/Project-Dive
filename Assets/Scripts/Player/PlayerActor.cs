using System;
using Helpers;
using Mechanics;
using Phys;
using Player;
using World;

using MyBox;

using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Audio;

[RequireComponent(typeof(PlayerStateMachine))]
[RequireComponent(typeof(PlayerInputController))]
public class PlayerActor : Actor {

    [Foldout("Move", true)]
    [SerializeField] private int MoveSpeed;
    [SerializeField] private int maxAcceleration;
    [SerializeField] private int maxAirAcceleration;
    [SerializeField] private int maxDeceleration;

    [Foldout("Jump", true)]
    [SerializeField] private int JumpHeight;
    [SerializeField] private int CrystalJumpHeight;
    [SerializeField] private int DoubleJumpHeight;
    [SerializeField] public float JumpCoyoteTime;
    [SerializeField] public float JumpBufferTime;
    [SerializeField, Range(0f, 1f)] public float JumpCutMultiplier;

    [Foldout("Dive", true)]
    [SerializeField] private int DiveVelocity;
    [SerializeField] private int DiveDeceleration;
    
    [Foldout("Dogo", true)]
    [SerializeField] private float DogoXJumpV;
    [SerializeField] private float DogoYJumpHeight;
    [SerializeField] public double DogoXVBufferTime;

    [Foldout("Misc", true)]
    [SerializeField, Range(0f, 1f)] private float roomTransitionVCutX = 0.5f;
    [SerializeField, Range(0f, 1f)] private float roomTransitionVCutY = 0.5f;

    private PlayerInputController _input;
    private PlayerStateMachine _stateMachine;

    private int _moveDirection;
    private bool _lastJumpBeingHeld;

    private void Awake()
    {
        _input = GetComponent<PlayerInputController>();
        _stateMachine = GetComponent<PlayerStateMachine>();
    }

    private void OnEnable()
    {
        Room.RoomTransitionEvent += OnRoomTransition;
    }

    private void OnDisable()
    {
        Room.RoomTransitionEvent -= OnRoomTransition;
    }

    private void Update()
    {
        UpdateInputs();

        // GetComponent<SpriteRenderer>().color = CheckCollisions(Vector2.down, e => true) ? Color.red : Color.blue;
    }

    private void UpdateInputs()
    {
        if (_input.JumpStarted())
        {
            _stateMachine.CurState.JumpPressed();
        }

        if (_input.JumpFinished())
        {
            _stateMachine.CurState.JumpReleased();
            _lastJumpBeingHeld = false;
        }

        if (_input.DiveStarted())
        {
            _stateMachine.CurState.DivePressed();
        }

        _moveDirection = _input.GetMovementInput();
    }

    public override void FixedUpdate() {
        base.FixedUpdate();
        bool grounded = IsGrounded();
        _stateMachine.CurState.SetGrounded(grounded);
        _stateMachine.CurState.MoveX(grounded);
    }

    public void MoveX(bool grounded) {
        int effectiveAcceleration;
        if (grounded) {
            effectiveAcceleration = _moveDirection == 0 ? maxDeceleration : maxAcceleration;
        } else {
            effectiveAcceleration = maxAirAcceleration;
        }

        int targetVelocityX = _moveDirection * MoveSpeed;
        int maxSpeedChange = (int) (effectiveAcceleration * Time.deltaTime);
        velocityX = Mathf.MoveTowards(velocityX, targetVelocityX, maxSpeedChange);
    }

    public double Dogo() {
        double v = velocityX;
        velocityX = 0;
        return v;
    }

    public override bool OnCollide(PhysObj p, Vector2 direction)
    {
        bool col = p.PlayerCollide(this, direction);
        if (direction.y < 0 && p.IsGround(this))
        {
            //SetGrounded(true);
        }
        else if (direction.y > 0 && col)
        {
            BonkHead();
        }

        return col;
    }

    public override bool PlayerCollide(PlayerActor p, Vector2 direction)
    {
        return false;
    }

    public override bool IsGround(PhysObj whosAsking)
    {
        return false;
    }

    public void Jump() {
        velocityY = GetJumpSpeedFromHeight(JumpHeight);
        _stateMachine.CurState.SetGrounded(false);
    }

    public void JumpFromInput() {
        _lastJumpBeingHeld = true;
        Jump();
    }
    
    public void CrystalJump() {
        velocityY = GetJumpSpeedFromHeight(CrystalJumpHeight);
        _stateMachine.CurState.SetGrounded(false);
        _lastJumpBeingHeld = false;
    }

    public void TryJumpCut()
    {
        if (_lastJumpBeingHeld && velocityY > 0f)
        {
            velocityY *= JumpCutMultiplier;
        }

        _lastJumpBeingHeld = false;
    }

    public void DoubleJump() {
        velocityY = GetJumpSpeedFromHeight(DoubleJumpHeight);
        _lastJumpBeingHeld = true;
        // If the player is trying to go in the opposite direction of their x velocity, instantly switch direction.
        if (_moveDirection != 0 && _moveDirection != Math.Sign(velocityX)) {
            velocityX = 0;
            print("lmao");
        }
    }

    public void DogoJump(bool conserveMomentum, double oldXV) {
        if (conserveMomentum) {
            if (_moveDirection == 1) {
                velocityX = (float)Math.Max(oldXV+DogoXJumpV, DogoXJumpV);
                print(oldXV);
            } else if (_moveDirection == -1) {
                velocityX = (float)Math.Min(oldXV-DogoXJumpV, -DogoXJumpV);
            }
        } else {
            velocityX = _moveDirection * DogoXJumpV;
            if (_moveDirection == 0) {
                velocityY = GetJumpSpeedFromHeight(DogoYJumpHeight);
                _lastJumpBeingHeld = false;
            }
        }
    }

    public void Land() {
        velocityY = 0;
    }

    public void Dive() {
        velocityY = DiveVelocity;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns>true if done diving</returns>
    public bool DiveDecelUpdate() {
        velocityY += DiveDeceleration;
        return velocityY > MaxFall;
    }

    public void Die() {
        transform.position = new Vector2(36, -118);
        velocity = Vector2.zero;
    }

    public bool EnterCrystal(Crystal c) {
        return _stateMachine.CurState.EnterCrystal(c);
    }

    public void BonkHead() {
        velocityY = Math.Min(10, velocityY);
    }

    public override bool Squish(PhysObj p, Vector2 d) {
        if (OnCollide(p, d)) {
            Debug.Log("Squish " + p);
            Die();
        }
        return false;
    }

    public bool IsDiving() {
        return _stateMachine.IsOnState<PlayerStateMachine.Diving>();
    }

    private void OnRoomTransition(Room roomEntering)
    {
        velocityX *= roomTransitionVCutX;
        velocityY *= roomTransitionVCutY;
    }

    private float GetJumpSpeedFromHeight(float jumpHeight)
    {
        return Mathf.Sqrt(-2f * GravityUp * jumpHeight);
    }

    private void OnDrawGizmosSelected() {
        Handles.Label(new Vector3(0, 56, 0) , "" + velocityY);
    }
}