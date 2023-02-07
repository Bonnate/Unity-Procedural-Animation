using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//MultiAimConstraint 를 사용
using UnityEngine.Animations.Rigging;

public class ProceduralAnimator : MonoBehaviour
{
    [Tooltip("프로시저 애니메이션을 시작하는 거리")]
    [SerializeField] private float mProcedureAnimDistance;

    [Tooltip("프로시저 애니메이션 컨트롤러를 담는 부모 트랜스폼")]
    [SerializeField] private Transform mProcedureAnimParent;

    /// <summary>
    /// 프로시저 애니메이션에서 바라볼 기본적인 타겟(배열 0번)
    /// </summary>
    private Transform mProcedureAnimBaseTaget;

    /// <summary>
    /// /// 프로시저 애니메이션 컨트롤러들
    /// </summary>
    private MultiAimConstraint[] mProcedureAnimControllers;

    /// <summary>
    /// 인스펙터에서 설정한 컨트롤러들의 초기 가중치 값
    /// </summary>
    private float[] mProcedureAnimOriginWeights;

    /// <summary>
    /// 대상을 바라보고 있는가?
    /// </summary>
    private bool mIsLookingTarget;

    /// <summary>
    /// 현재 바라보게하지 않는 코루틴이 실행 가능 상태인가?
    /// </summary>
    private bool mIsBackwardWeightCorReady;

    /// <summary>
    /// 어떠한 대상을 무조건적으로 바라보는가?
    /// </summary>
    private bool mIsForceLookTarget;

    /// <summary>
    /// 대상을 바라보는것을 활성화 하는가?
    /// </summary>
    [HideInInspector] public bool IsEnableTargetLook = true;

    /// <summary>
    /// 컨트롤러의 개수
    /// </summary>
    /// <value></value>
    private int ControllerLength
    {
        get
        {
            return mProcedureAnimControllers.Length;
        }
    }

    private Coroutine mWeightCoroutine, mLookAtCoroutine;

    //프로시저 애니메이션 초기화
    private void Awake()
    {
        //컨트롤러(MultiAimConstraint)들을 가져오기
        mProcedureAnimControllers = mProcedureAnimParent.GetComponentsInChildren<MultiAimConstraint>();

        //바라볼 타겟을 등록
        mProcedureAnimBaseTaget = mProcedureAnimControllers[0].data.sourceObjects.GetTransform(0);

        //각 컨트롤러(MultiAimConstraint)의 초기 가중치 등록 및 가중치 초기화
        mProcedureAnimOriginWeights = new float[ControllerLength];
        for (int i = 0; i < ControllerLength; ++i)
        {
            //등록
            mProcedureAnimOriginWeights[i] = mProcedureAnimControllers[i].weight;

            //0으로 초기화
            mProcedureAnimControllers[i].weight = 0f;
        }

        //플래그 초기화
        mIsForceLookTarget = mIsBackwardWeightCorReady = mIsLookingTarget = false;
        IsEnableTargetLook = true;
    }

    private void Update()
    {
        //설정된 거리보다 타겟까지의 거리가 가까우거나 || 플래그에 의해 무조건 보게하는경우
        if (IsEnableTargetLook && (transform.position - mProcedureAnimBaseTaget.position).magnitude < mProcedureAnimDistance || mIsForceLookTarget)
        {
            //만약 현재 이미 보고있는경우에는 리턴
            if (mIsLookingTarget) { return; }

            //되돌아갈 코루틴을 준비하고, 현재 보고 있다고 플래그
            mIsBackwardWeightCorReady = mIsLookingTarget = true;

            //바라보도록 코루틴 실행
            LookBaseTarget(true);
        }
        //설정된 거리보다 멀어지면 바라보게 하지 않음
        else
        {
            //이미 코루틴이 실행된 경우에는 중복실행하지 않도록 방지
            if (!mIsBackwardWeightCorReady) { return; }

            //코루틴 준비상태 해제(이미 실행중이거나 실행됨)
            mIsBackwardWeightCorReady = false;

            //바라보지 않도록 코루틴 실행
            LookBaseTarget(false);
        }
    }

    private void LookBaseTarget(bool isForward)
    {
        if (mWeightCoroutine != null) { StopCoroutine(mWeightCoroutine); }
        mWeightCoroutine = StartCoroutine(COR_FadeWeight(isForward));
    }

    /// https://forum.unity.com/threads/cant-use-aimconstraint-data-sourceobjects-setweight.882739/
    /// <summary>
    /// 특정한 오브젝트를 바라보게 한다.
    /// </summary>
    /// <param name="targetTransform">바라볼 트랜스폼</param>
    /// <param name="duration">몇초동안 바라볼것인가?</param>
    /// <param name="lookTransitionSpeed">해당 트랜스폼을 몇초에 걸쳐 바라보게 할것인가?</param>
    /// <param name="releaseTransitionSpeed">duration이 끝난 후 몇초에 걸쳐 돌아올것인가?</param>
    public void LookTarget(Transform targetTransform, float duration = 3.0f, float lookTransitionSpeed = 1.0f, float releaseTransitionSpeed = 5.0f)
    {
        if (mLookAtCoroutine != null) { StopCoroutine(mLookAtCoroutine); }
        mLookAtCoroutine = StartCoroutine(COR_LookTargetLerp(targetTransform, duration, lookTransitionSpeed, releaseTransitionSpeed));
    }

    /// <summary>
    /// LookTarget에 의해서 타겟을 바라보게 하는 코루틴
    /// </summary>
    private IEnumerator COR_LookTargetLerp(Transform targetTransform, float duration, float lookTransitionSpeed, float releaseTransitionSpeed)
    {
        //무조건적으로 바라보도록 플래그
        mIsForceLookTarget = true;

        //가중치트랜스폼 배열 생성
        WeightedTransformArray[] weightArray = new WeightedTransformArray[ControllerLength];

        //코루틴 중복실행시 자연스러운 연출을 위해 호출 직전 또는 현재 가중치값을 가져옴
        float[] currentBaseWeightArray = new float[ControllerLength];
        float[] currentTargetWeightArray = new float[ControllerLength];
        bool isTargetAvailable = false;

        //생성된 각 가중치 트랜스폼에 컨트롤러의 가중치 값을 넣음
        for (int i = 0; i < ControllerLength; ++i)
        {
            weightArray[i] = mProcedureAnimControllers[i].data.sourceObjects;
            currentBaseWeightArray[i] = (weightArray[i].GetWeight(0) == 1.0f) ? mIsLookingTarget ? 1 : 0 : weightArray[i].GetWeight(0);

            //코루틴이 중첩실행되어 배열이 두개 이상인경우 기존 배열들을 모두 제거
            if (weightArray[i].Count >= 2)
            {
                //단 하나라도 weightArray >=2라면 isTargetAvailable는 true;
                isTargetAvailable = true;
                //파괴 직전의 가중치를 가져옴
                currentTargetWeightArray[i] = weightArray[i].GetWeight(1);

                for (int j = weightArray[i].Count - 1; j >= 1; --j)
                {
                    weightArray[i].RemoveAt(j);
                }
            }

            //새로 바라볼 생성된 트랜스폼을 추가
            weightArray[i].Add(new WeightedTransform(targetTransform, 0));
            weightArray[i].SetTransform(1, targetTransform);

            //트랜스폼이 추가된 배열 추가
            mProcedureAnimControllers[i].data.sourceObjects = weightArray[i];
        }

        //새롭게 구성된 가중치 트랜스폼으로 빌드
        //https://forum.unity.com/threads/multi-aim-constraint-set-source-at-runtime.944559/
        GetComponent<RigBuilder>().Build();

        //진행도 생성 (가중치 반전시키기)
        float process = 0f;

        while (process < 1f)
        {
            process += Time.deltaTime / lookTransitionSpeed;

            for (int i = 0; i < ControllerLength; ++i)
            {
                //각 배열의 가중치를 선형으로 대칭 반전
                weightArray[i].SetWeight(0, Mathf.Lerp(currentBaseWeightArray[i], 0f, process));
                weightArray[i].SetWeight(1, Mathf.Lerp(isTargetAvailable ? currentTargetWeightArray[i] : 0f, 1f, process));

                mProcedureAnimControllers[i].data.sourceObjects = weightArray[i];
            }

            yield return null;
        }

        yield return new WaitForSeconds(duration);


        //진행도 초기화 (가중치 초기 상태로 다시 되돌리기)
        process = 0f;

        while (process < 1f)
        {
            process += Time.deltaTime / releaseTransitionSpeed;

            for (int i = 0; i < ControllerLength; ++i)
            {
                //각 배열의 가중치를 선형으로 대칭 반전
                weightArray[i].SetWeight(0, Mathf.Lerp(0f, mIsLookingTarget ? 1f : 0f, process));
                weightArray[i].SetWeight(1, Mathf.Lerp(1f, 0f, process));

                mProcedureAnimControllers[i].data.sourceObjects = weightArray[i];
            }

            yield return null;
        }

        //임시로 생성됐던 1번 인덱스를 제거하고 초기 상태로 되돌린다
        for (int i = 0; i < ControllerLength; ++i)
        {
            weightArray[i].RemoveAt(1);

            mProcedureAnimControllers[i].data.sourceObjects = weightArray[i];
        }

        //무조건적으로 바라보도록 하는 플래그 해제
        mIsForceLookTarget = false;
    }

    /// <summary>
    /// 가중치를 조절하는 코루틴
    /// </summary>
    /// <param name="isForward">가중치를 올려 활성화하는가?</param>
    /// <returns></returns>
    private IEnumerator COR_FadeWeight(bool isForward)
    {
        //isForward가 false인경우 바라보는중 해제 플래그
        if (!isForward) { mIsLookingTarget = false; }

        float[] currentProcedureAnimWeights = new float[ControllerLength];
        float process = 0f;

        for (int i = 0; i < ControllerLength; ++i) { currentProcedureAnimWeights[i] = mProcedureAnimControllers[i].weight; }

        while (true)
        {
            process += Time.deltaTime;
            for (int i = 0; i < ControllerLength; ++i) { mProcedureAnimControllers[i].weight = Mathf.Lerp(currentProcedureAnimWeights[i], isForward ? mProcedureAnimOriginWeights[i] : 0f, process); }

            if (process > 1f)
            {
                yield break;
            }

            yield return null;
        }
    }
}