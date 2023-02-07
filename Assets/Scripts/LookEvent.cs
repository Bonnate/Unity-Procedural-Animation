using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LookEvent : MonoBehaviour
{
    [SerializeField] private ProceduralAnimator mNPC1ProcedureAnim;
    [SerializeField] private Transform mLookTarget;

    void OnGUI()
    {
        if (GUI.Button(new Rect(50, 50, Screen.width * 0.2f, Screen.width * 0.1f), "Look Event Target"))
        {
            mNPC1ProcedureAnim.LookTarget(mLookTarget, 5, 1.0f, 5.0f);
        }
    }
}
