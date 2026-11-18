using UnityEngine;

namespace BattlefieldPrototype
{
    /// <summary>Articulated visual motion. Never moves the character motor or the camera.</summary>
    [DefaultExecutionOrder(250)]
    public sealed class BattleUnitPresentation : MonoBehaviour
    {
        BattleUnit unit;
        Transform torso, leftLeg, rightLeg, leftArm, rightArm;
        Transform leftKnee, rightKnee, leftElbow, rightElbow;
        bool quadruped, spirit;
        Vector3 previousPosition;
        float stride, gait, previousAttack, swing;

        public void Initialize(BattleUnit owner, Transform body, Transform legL, Transform legR, Transform armL, Transform armR)
        {
            unit = owner; torso = body;
            leftLeg = legL; rightLeg = legR; leftArm = armL; rightArm = armR;
            previousPosition = transform.position;
        }

        public void Initialize(BattleUnit owner, BattleCharacterModel model)
        {
            Initialize(owner, model.Body, model.LeftLeg, model.RightLeg, model.LeftArm, model.RightArm);
            leftKnee = model.LeftKnee; rightKnee = model.RightKnee;
            leftElbow = model.LeftElbow; rightElbow = model.RightElbow;
            quadruped = model.IsQuadruped; spirit = model.IsSpirit;
        }

        void LateUpdate()
        {
            if (unit == null || torso == null) return;
            float dt = Mathf.Max(Time.deltaTime, .0001f);
            float speed = Vector3.ProjectOnPlane(transform.position - previousPosition, Vector3.up).magnitude / dt;
            previousPosition = transform.position;
            bool active = unit.IsAlive && !unit.IsPossessed && unit.Director != null && unit.Director.IsBattleRunning;
            gait = Mathf.MoveTowards(gait, active ? Mathf.Clamp01(speed / 2.4f) : 0, dt * 8);
            stride += Mathf.Min(speed, 7) * dt * 3.8f;
            float step = Mathf.Sin(stride) * gait;
            if (spirit)
            {
                torso.localPosition = Vector3.up * (Mathf.Sin(Time.time * 2.1f) * .055f);
                torso.localRotation = Quaternion.Euler(0, Time.time * 19, Mathf.Sin(Time.time * 1.6f) * 5);
                return;
            }
            leftLeg.localRotation = Quaternion.Euler(step * 27, 0, -1.5f);
            rightLeg.localRotation = Quaternion.Euler(-step * 27, 0, 1.5f);
            if (leftKnee != null) leftKnee.localRotation = Quaternion.Euler(Mathf.Max(0, -step) * 36, 0, 0);
            if (rightKnee != null) rightKnee.localRotation = Quaternion.Euler(Mathf.Max(0, step) * 36, 0, 0);
            if (unit.AttackCooldownRemaining > previousAttack + .02f && unit.IsMelee) swing = .28f;
            previousAttack = unit.AttackCooldownRemaining;
            swing = Mathf.Max(0, swing - dt);
            float strike = Mathf.Sin((1 - swing / .28f) * Mathf.PI) * (swing > 0 ? 65 : 0);
            leftArm.localRotation = Quaternion.Euler(-step * 18 - (unit.IsMelee ? strike * .5f : 12), 0, quadruped ? 0 : -5);
            rightArm.localRotation = Quaternion.Euler(step * 18 - (unit.IsMelee ? strike : 18), 0, quadruped ? 0 : 5);
            float breathing = Mathf.Sin(Time.time * 1.9f + transform.GetSiblingIndex()) * (active ? .003f : .005f);
            if (leftElbow != null) leftElbow.localRotation = Quaternion.Euler(-14 - Mathf.Abs(step) * 9 - strike * .2f, 0, 0);
            if (rightElbow != null) rightElbow.localRotation = Quaternion.Euler(-18 - Mathf.Abs(step) * 9 - strike * .35f, 0, 0);
            torso.localPosition = new Vector3(0, Mathf.Abs(Mathf.Cos(stride)) * gait * .018f + breathing, 0);
            torso.localRotation = Quaternion.Euler(gait * 2, step * 2.5f, -step * 1.1f);
        }
    }
}
