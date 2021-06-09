/*
Copyright 2019 Gfi Informatique

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

    http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

using BrowserDesktop.Controller;
using BrowserDesktop.Cursor;
using BrowserDesktop.Menu;
using System.Collections;
using umi3d.cdk;
using umi3d.common;
using UnityEngine;

using UnityEngine.AI;

public class FpsNavigation : AbstractNavigation
{
    public Transform _viewpoint;
    public Transform _neckPivot;
    public float maxAngle;
    public Transform head;
    public Transform Body;
    public Transform Node;
    public Transform TorsoUpAnchor;

    /// <summary>
    /// Agent to limit user's movements.
    /// </summary>
    public NavMeshAgent agent;

    /// <summary>
    /// Current ground height.
    /// </summary>
    private float baseHeight;

    bool isActive = false;
    public FpsScriptableAsset data;

    bool navigateTo = false;
    Vector3 destination;

    public enum State { Default, FreeHead, FreeMousse }
    public enum Navigation { Walking, Flying }

    public State state;
    bool changeToDefault = false;
    Vector3 LastAngleView;
    public Navigation navigation;

    float MaxJumpVelocity;
    float MinJumpVelocity;

    struct JumpData
    {
        public bool jumping;
        public float timeSinceJump;
        public float velocity;
        public float deltaHeight;
        public float heigth;

        public JumpData(bool jumping, float timeSinceJump, float velocity, float deltaHeight) : this()
        {
            this.jumping = jumping;
            this.timeSinceJump = timeSinceJump;
            this.velocity = velocity;
            this.deltaHeight = deltaHeight;
        }
    }
    JumpData jumpData;

    #region abstract
    public override void Activate()
    {
        isActive = true;
        navigateTo = false;
        state = State.Default;
        jumpData = new JumpData();
        //CursorHandler.Movement = CursorHandler.CursorMovement.Center;

        MaxJumpVelocity = Mathf.Sqrt(2 * Mathf.Abs(data.gravity) * data.MaxJumpHeight);
        MinJumpVelocity = Mathf.Sqrt(2 * Mathf.Abs(data.gravity) * data.MinJumpHeight);
    }

    public override void Disable()
    {
        isActive = false;
    }

    public override void Navigate(NavigateDto data)
    {
        navigateTo = true;
        destination = data.position;
    }

    public override void Teleport(TeleportDto data)
    {
        agent.enabled = false;
        Node.position = data.position;
        Node.rotation = data.rotation;
        baseHeight = data.position.Y;

        StartCoroutine(ResetNavmeshAgent());
    }

    IEnumerator ResetNavmeshAgent ()
    {
        yield return null;
        agent.enabled = true;
    } 

    #endregion

    float ComputeJump(bool jumping)
    {
        if (jumpData.jumping && jumpData.deltaHeight == 0)
        {
            jumpData.velocity = MaxJumpVelocity;
        }

        if (jumpData.jumping != jumping)
        {
            jumpData.jumping = jumping;

            if (!jumpData.jumping && jumpData.velocity > MinJumpVelocity)
            {
                jumpData.velocity = MinJumpVelocity;
            }

        }

        jumpData.velocity += data.gravity * Time.deltaTime;
        jumpData.deltaHeight = Mathf.Max(0, jumpData.deltaHeight + jumpData.velocity * Time.deltaTime);
        return jumpData.deltaHeight;
    }

    private void Update()
    {
        if (!isActive)
            return;

        if (agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            NavMeshHit hit;
            if(NavMesh.SamplePosition(agent.transform.position, out hit, .2f, NavMesh.AllAreas)){
                baseHeight = hit.position.y;
            }
        }

        if (Input.GetKeyDown(InputLayoutManager.GetInputCode(InputLayoutManager.Input.MainMenuToggle)))
        {
            PauseMenu.ToggleDisplay();
        }

        if (SideMenu.IsExpanded || CursorHandler.Movement == CursorHandler.CursorMovement.Free || CursorHandler.Movement == CursorHandler.CursorMovement.FreeHiden)
        {
            Vector3 position = Vector3.zero;// Node.transform.position;
            position.y = jumpData.heigth + baseHeight;
            Body.transform.localPosition = position;
            return;
        }

        if (TextInputDisplayerElement.isTyping)
            return;

        if (state == State.Default && Input.GetKey(InputLayoutManager.GetInputCode(InputLayoutManager.Input.FreeView))) { state = State.FreeHead; }
        else if (state == State.FreeHead && !Input.GetKey(InputLayoutManager.GetInputCode(InputLayoutManager.Input.FreeView))) { state = State.Default; changeToDefault = true; }
        Vector2 Move = Vector2.zero;
        float height = jumpData.heigth;

        if (navigateTo)
        {
            var delta = destination - Node.transform.position;
            Move = delta.normalized;
        }
        else
        {
            if (Input.GetKey(InputLayoutManager.GetInputCode(InputLayoutManager.Input.Forward))) { Move.x += 1; }
            if (Input.GetKey(InputLayoutManager.GetInputCode(InputLayoutManager.Input.Backward))) { Move.x -= 1; }
            if (Input.GetKey(InputLayoutManager.GetInputCode(InputLayoutManager.Input.Right))) { Move.y += 1; }
            if (Input.GetKey(InputLayoutManager.GetInputCode(InputLayoutManager.Input.Left))) { Move.y -= 1; }
        }

        switch (navigation)
        {
            case Navigation.Walking:
                Walk(ref Move, ref height);
                jumpData.heigth = height;
                height += jumpData.deltaHeight;
                break;
            case Navigation.Flying:
                Fly(ref Move, ref height);
                jumpData.heigth = height;
                break;
        }
        Move *= Time.deltaTime;

        HandleView();
        Vector3 pos = Node.rotation * new Vector3(Move.y, 0, Move.x);
        pos += Node.transform.position;
        Node.transform.position = pos;

        Vector3 bodyPose = Vector3.zero;
        bodyPose.y = height + baseHeight;
        Body.transform.localPosition = bodyPose;
    }

    void Walk(ref Vector2 Move, ref float height)
    {
        if (Input.GetKey(InputLayoutManager.GetInputCode(InputLayoutManager.Input.Squat)))
        {
            Move.x *= (Move.x > 0) ? data.forwardSpeed.y : data.backwardSpeed.y;
            Move.y *= data.lateralSpeed.y;
        }
        else if (Input.GetKey(InputLayoutManager.GetInputCode(InputLayoutManager.Input.Sprint)))
        {
            Move.x *= (Move.x > 0) ? data.forwardSpeed.z : data.backwardSpeed.z;
            Move.y *= data.lateralSpeed.z;
        }
        else
        {
            Move.x *= (Move.x > 0) ? data.forwardSpeed.x : data.backwardSpeed.x;
            Move.y *= data.lateralSpeed.x;
        }
        bool Squatting = Input.GetKey(InputLayoutManager.GetInputCode(InputLayoutManager.Input.Squat));
        height = Mathf.Lerp(height, (Squatting) ? data.squatHeight : data.standHeight, data.squatSpeed == 0 ? 1000000 : Time.deltaTime / data.squatSpeed);
        TorsoUpAnchor.localRotation = Quaternion.Euler((Squatting) ? data.squatTorsoAngle : 0, 0, 0);
        ComputeJump(Input.GetKey(InputLayoutManager.GetInputCode(InputLayoutManager.Input.Jump)));
    }

    void Fly(ref Vector2 Move, ref float height)
    {
        Move.x *= data.flyingSpeed;
        Move.y *= data.flyingSpeed;
        height += data.flyingSpeed * ((Input.GetKey(InputLayoutManager.GetInputCode(InputLayoutManager.Input.Squat)) ? -1 : 0) + (Input.GetKey(InputLayoutManager.GetInputCode(InputLayoutManager.Input.Jump)) ? 1 : 0));
    }

    void HandleView()
    {
        if (state == State.FreeMousse) return;
        Vector3 angleView = NormalizeAngle(_viewpoint.rotation.eulerAngles);

        Vector2 angularSpeed = new Vector2(-1 * Input.GetAxis("Mouse Y") * data.AngularViewSpeed.x, Input.GetAxis("Mouse X") * data.AngularViewSpeed.y);
        Vector3 result = NormalizeAngle(angleView + (Vector3)angularSpeed);
        if (changeToDefault)
        {
            result = LastAngleView;
            changeToDefault = false;
        }
        Vector3 displayResult;

        if (result.x < data.XAngleRange.x) result.x = data.XAngleRange.x;
        if (result.x > data.XAngleRange.y) result.x = data.XAngleRange.y;
        displayResult = result;
        if (displayResult.x < data.XDisplayAngleRange.x) displayResult.x = data.XDisplayAngleRange.x;
        if (displayResult.x > data.XDisplayAngleRange.y) displayResult.x = data.XDisplayAngleRange.y;

        if (state == State.Default)
        {
            Node.transform.rotation = Quaternion.Euler(new Vector3(0, result.y, 0));
            LastAngleView = result;
        }
        else
        {
            Vector3 angleNeck = NormalizeAngle(Node.rotation.eulerAngles);
            float delta = Mathf.DeltaAngle(result.y, angleNeck.y);

            if (delta < data.YAngleRange.x) result.y = -data.YAngleRange.x + angleNeck.y;
            if (delta > data.YAngleRange.y) result.y = -data.YAngleRange.y + angleNeck.y;
        }
        _viewpoint.transform.rotation = Quaternion.Euler(result);
        _neckPivot.transform.rotation = Quaternion.Euler(new Vector3(Mathf.Clamp(result.x, -maxAngle, maxAngle), result.y, result.z));
        head.transform.rotation = Quaternion.Euler(displayResult);
    }

    Vector3 NormalizeAngle(Vector3 angle)
    {
        angle.x = Mathf.DeltaAngle(0, angle.x);
        angle.y = Mathf.DeltaAngle(0, angle.y);
        angle.z = Mathf.DeltaAngle(0, angle.z);
        return angle;
    }

}
