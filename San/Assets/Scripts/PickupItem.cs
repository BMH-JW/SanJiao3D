using UnityEngine;

/// <summary>
/// �����ɵ��ʰȡ����Ʒ�����ӡ�������ң�����ȣ���
/// ��һ�ε������Ʒ�Ƶ��ӽ����룬��һֱ�����ӽǣ�
/// �ٵ��һ�Σ���Ʒƽ���ص����ǰ��λ�ú���ת��
/// �������� HandleClick ͳһ�����ߴ��������ű�����Ҫ�Լ�����ꡣ
/// </summary>
[DisallowMultipleComponent]
public class PickupItem : MonoBehaviour
{
    [Header("ʰȡλ��")]
    [Tooltip("��Ʒͣ�������ǰ����Զ���ף���������������彨�� 0.8~1.0")]
    public float focusDistance = 0.8f;

    [Tooltip("����������ĵ����������ף���������ȫ��ס׼��")]
    public float focusVerticalOffset = -0.03f;

    [Header("����")]
    [Tooltip("�ƶ����ӽ�������ٶ�")]
    public float moveSpeed = 14f;

    [Tooltip("�Ż�ԭλʱ���ٶ�")]
    public float returnSpeed = 12f;

    [Tooltip("ʰȡʱ�Ƿ�������ת������Ʒ���Ծ�ͷ")]
    public bool faceCamera = false;

    [Tooltip("��Ҫ�����������ʱ���������ģ�����泯 -Z ���� 0,180,0��")]
    public Vector3 heldRotationEuler = Vector3.zero;

    [Header("����")]
    [Tooltip("��Ʒ���� Rigidbody ʱ��ʰȡ�ڼ���ʱ��Ϊ Kinematic���Żغ�ָ�")]
    public bool handleRigidbody = true;

    [Header("��ײ��")]
    [Tooltip("û����ײ��ʱ��ģ�Ͱ�Χ���Զ���һ������֤�ܱ��㵽")]
    public bool autoAddCollider = true;

    [Header("����")]
    public bool debugLog = true;

    public bool IsHeld { get; private set; }

    // ȫ��ֻ������һ�������µĻ��Զ��ѾɵķŻ�
    private static PickupItem currentHeld;

    // �� �ؿ��������أ�Ĭ�Ϲرգ���������ؿ�ʱ�� LivingRoomQuest ���� SetPickupEnabled(true) ����
    public static bool PickupEnabled { get; private set; } = false;

    private Transform originalParent;
    private Vector3 originalWorldPos;
    private Quaternion originalWorldRot;
    private Rigidbody rb;
    private bool rigidbodyWasKinematic;
    private Camera cam;
    private bool returning;

    private void Awake()
    {
        if (autoAddCollider) EnsureCollider();
        rb = GetComponent<Rigidbody>();
    }

    /// <summary>����/�ر�ȫ��ʰȡ���ص�ʱ����������Ʒ���¡�</summary>
    public static void SetPickupEnabled(bool value)
    {
        PickupEnabled = value;

        if (!value && currentHeld != null)
            currentHeld.PutBack();
    }

    /// <summary>����յ�ʱ�� HandleClick ���ã������������Ʒ��</summary>
    public static void PutBackHeld()
    {
        if (currentHeld != null)
            currentHeld.PutBack();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetPickupGate()
    {
        PickupEnabled = false;
        currentHeld = null;
    }

    /// <summary>�� HandleClick ��������ʱ���á�</summary>
    public void TogglePickup(Camera camera)
    {
        // �ؿ����ƣ�û��������ؿ�֮ǰ������Ҳ������
        if (!PickupEnabled)
        {
            if (debugLog)
                Debug.Log("[PickupItem] ��ǰ�ؿ�������ʰȡ��Ʒ", this);
            return;
        }

        // ���ڷɻ�ԭλʱ���Ե�����������س鴤
        if (returning) return;

        // �����Ѿ��б����Ʒ��
        // ������Ʒ����"����"�����ǰ�������������
        if (currentHeld != null && currentHeld != this)
        {
            currentHeld.PutBack();
            return;
        }

        Camera resolved = camera != null && camera.isActiveAndEnabled
            ? camera
            : ResolveActiveCamera();

        if (resolved == null) return;

        // ֻ������ҵ�һ�˳����ʰȡ������ 2D С��Ϸ����ʱ��ʰȡ 3D ��Ʒ
        PlayerMover mover = FindObjectOfType<PlayerMover>();
        if (mover != null && !resolved.transform.IsChildOf(mover.transform))
            return;

        cam = resolved;

        if (!IsHeld)
            PickUp();
        else
            PutBack();
    }

    private void LateUpdate()
    {
        if (!IsHeld) return;

        if (returning)
        {
            ReturnToOrigin();
            return;
        }

        if (cam == null || !cam.isActiveAndEnabled)
            cam = ResolveActiveCamera();

        if (cam == null) return;

        Vector3 targetPos = cam.transform.position
            + cam.transform.forward * focusDistance
            + cam.transform.up * focusVerticalOffset;

        float t = 1f - Mathf.Exp(-moveSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, targetPos, t);

        if (faceCamera)
        {
            Quaternion targetRot =
                cam.transform.rotation * Quaternion.Euler(heldRotationEuler);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
        }
    }

    private void PickUp()
    {
        // �Ѿ����ű����Ʒʱ���ȰѾɵķŻ�ȥ�����������ߵ��������Ѵ�����
        if (currentHeld != null && currentHeld != this)
            currentHeld.PutBack();

        originalParent = transform.parent;
        originalWorldPos = transform.position;
        originalWorldRot = transform.rotation;

        if (handleRigidbody && rb != null)
        {
            rigidbodyWasKinematic = rb.isKinematic;
            rb.isKinematic = true;
        }

        // ����ԭ����������Ҿ��ƶ�ʱ����Ʒһ�����
        transform.SetParent(null, true);

        IsHeld = true;
        returning = false;
        currentHeld = this;

        if (debugLog)
            Debug.Log("[PickupItem] ʰȡ��" + name, this);
    }

    /// <summary>�Ż�ԭλ��Ҳ�������ⲿ���á�</summary>
    public void PutBack()
    {
        if (!IsHeld || returning) return;

        returning = true;
        if (currentHeld == this) currentHeld = null;

        if (debugLog)
            Debug.Log("[PickupItem] �Ż�ԭλ��" + name, this);
    }

    private void ReturnToOrigin()
    {
        float t = 1f - Mathf.Exp(-returnSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, originalWorldPos, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, originalWorldRot, t);

        // ��û���ͼ�����
        if ((transform.position - originalWorldPos).sqrMagnitude > 0.0004f)
            return;

        transform.SetParent(originalParent, true);
        transform.position = originalWorldPos;
        transform.rotation = originalWorldRot;

        if (handleRigidbody && rb != null)
            rb.isKinematic = rigidbodyWasKinematic;

        returning = false;
        IsHeld = false;

        if (debugLog)
            Debug.Log("[PickupItem] �ѷŻأ�" + name, this);
    }

    private static Camera ResolveActiveCamera()
    {
        PlayerMover mover = FindObjectOfType<PlayerMover>();
        if (mover != null)
        {
            foreach (Camera c in mover.GetComponentsInChildren<Camera>(true))
                if (c.isActiveAndEnabled) return c;
        }

        if (Camera.main != null) return Camera.main;

        foreach (Camera c in Camera.allCameras)
            if (c.isActiveAndEnabled) return c;

        return null;
    }

    private void EnsureCollider()
    {
        // ֻ�������е���ײ�壺��Щ����ģ���Դ�����ײ���ǽ���״̬�����ߴ򲻵�
        Collider[] existing = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < existing.Length; i++)
            if (existing[i].enabled) return;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            if (debugLog)
                Debug.LogWarning("[PickupItem] " + name +
                    " û�������е���ײ�壬Ҳû�� Renderer���޷���������������ҵ���ģ�͵������ϣ����ֶ���һ�� Collider��", this);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        BoxCollider box = gameObject.AddComponent<BoxCollider>();
        box.center = transform.InverseTransformPoint(bounds.center);

        Vector3 lossy = transform.lossyScale;
        box.size = new Vector3(
            bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(lossy.x)),
            bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(lossy.y)),
            bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(lossy.z)));

        if (debugLog)
            Debug.Log("[PickupItem] ��Ϊ " + name + " �Զ��������ײ��", this);
    }

    /// <summary>ģ��������ռ�İ�Χ�У������жϵ�������ĸ���Ʒ�ϡ�</summary>
    public bool TryGetWorldBounds(out Bounds bounds)
    {
        bounds = new Bounds(transform.position, Vector3.zero);

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool has = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || r is ParticleSystemRenderer) continue;

            if (!has) { bounds = r.bounds; has = true; }
            else bounds.Encapsulate(r.bounds);
        }

        return has;
    }

    /// <summary>
    /// �������к��Ҷ�Ӧ�� PickupItem��
    /// ����������������/������������е������������ײ�壨ɳ�������ӡ�����ģ�͵ĺϲ���ײ�壩��
    /// �������ϣ��Ұ�Χ������������������ݲΧ�ڵ���Ʒ��
    /// </summary>
    public static PickupItem FindAtClick(RaycastHit hit, float tolerance)
    {
        PickupItem direct = hit.collider.GetComponentInParent<PickupItem>();
        if (direct != null) return direct;

        Transform t = hit.collider.transform;
        float bestSqr = tolerance * tolerance;

        while (t != null)
        {
            PickupItem best = null;
            PickupItem[] items = t.GetComponentsInChildren<PickupItem>(true);

            for (int i = 0; i < items.Length; i++)
            {
                PickupItem item = items[i];
                if (item == null || !item.isActiveAndEnabled) continue;
                if (!item.TryGetWorldBounds(out Bounds bounds)) continue;

                float d = bounds.SqrDistance(hit.point);
                if (d <= bestSqr)
                {
                    bestSqr = d;
                    best = item;
                }
            }

            if (best != null) return best;
            t = t.parent;
        }

        return null;
    }
}