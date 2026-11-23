using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 音频裁剪器：显示波形，拖动选区，试听选段，导出为 WAV。
/// 菜单：Tools/音频裁剪器
/// </summary>
public class AudioTrimmerWindow : EditorWindow
{
    private AudioClip clip;

    // 交错排列的原始采样
    private float[] samples;

    // 降成单声道后的数据，只用于画波形
    private float[] mono;

    private float startTime;
    private float endTime;

    private string outputName = "";

    private Texture2D waveformTex;
    private int waveformTexWidth;
    private int waveformTexHeight;

    private bool draggingStart;
    private bool draggingEnd;

    private bool previewing;
    private double previewBeginTime;

    private static Type audioUtilType;
    private static MethodInfo playPreviewClipMethod;
    private static MethodInfo stopAllPreviewClipsMethod;
    private static bool audioUtilInit;

    [MenuItem("Tools/音频裁剪器")]
    public static void Open()
    {
        AudioTrimmerWindow window =
            GetWindow<AudioTrimmerWindow>("音频裁剪器");

        window.minSize = new Vector2(560f, 430f);
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;

        StopPreview();
        DestroyWaveformTexture();
    }

    private void OnDestroy()
    {
        DestroyWaveformTexture();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(6f);

        AudioClip newClip = (AudioClip)EditorGUILayout.ObjectField(
            "音频文件",
            clip,
            typeof(AudioClip),
            false);

        if (newClip != clip)
            SetClip(newClip);

        if (clip == null)
        {
            EditorGUILayout.HelpBox(
                "把 Assets/Music 里的音频拖到这里（例如 结尾.mp3），" +
                "然后拖动波形上的两条线选择片段。",
                MessageType.Info);
            return;
        }

        if (samples == null)
        {
            EditorGUILayout.HelpBox(
                "无法读取音频采样。请在音频的 Import Settings 里把 " +
                "Load Type 设为 Decompress On Load，或点下面的按钮自动修改。",
                MessageType.Warning);

            if (GUILayout.Button("修改 Import Settings 并重新读取"))
                FixImportSettings();

            return;
        }

        EditorGUILayout.LabelField(
            "时长",
            clip.length.ToString("0.00") + " 秒    采样率 " +
            clip.frequency + " Hz    声道 " + clip.channels);

        Rect waveRect = EditorGUILayout.GetControlRect(
            false,
            160f,
            GUILayout.ExpandWidth(true));

        DrawWaveform(waveRect);
        HandleWaveformInput(waveRect);

        EditorGUILayout.Space(4f);

        EditorGUILayout.MinMaxSlider(
            "选区",
            ref startTime,
            ref endTime,
            0f,
            clip.length);

        EditorGUILayout.BeginHorizontal();
        startTime = EditorGUILayout.FloatField("开始（秒）", startTime);
        endTime = EditorGUILayout.FloatField("结束（秒）", endTime);
        EditorGUILayout.EndHorizontal();

        startTime = Mathf.Clamp(
            startTime,
            0f,
            Mathf.Max(0f, clip.length - 0.02f));

        endTime = Mathf.Clamp(
            endTime,
            startTime + 0.02f,
            Mathf.Max(0.02f, clip.length));

        EditorGUILayout.LabelField(
            "选中长度",
            (endTime - startTime).ToString("0.00") + " 秒");

        EditorGUILayout.Space(6f);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(
            previewing ? "停止试听" : "试听选段",
            GUILayout.Height(26f)))
        {
            if (previewing) StopPreview();
            else StartPreview();
        }

        if (GUILayout.Button("导出为 WAV", GUILayout.Height(26f)))
            Export();

        EditorGUILayout.EndHorizontal();

        outputName = EditorGUILayout.TextField("导出文件名", outputName);

        EditorGUILayout.HelpBox(
            "会导出到 Assets/Music/，文件名重复时自动编号。" +
            "导出后把 WAV 拖到需要的地方（例如闭幕音乐的 Ending Music）即可。",
            MessageType.None);
    }

    // ====================================================================
    // 载入音频
    // ====================================================================

    private void SetClip(AudioClip newClip)
    {
        StopPreview();

        clip = newClip;
        samples = null;
        mono = null;

        DestroyWaveformTexture();

        if (clip == null) return;

        startTime = 0f;
        endTime = clip.length;
        outputName = clip.name + "_trim";

        LoadClipData();
    }

    private void LoadClipData()
    {
        samples = null;
        mono = null;

        if (clip == null) return;

        if (clip.loadState != AudioDataLoadState.Loaded)
            clip.LoadAudioData();

        int total = clip.samples * clip.channels;

        if (total <= 0) return;

        float[] data = new float[total];

        try
        {
            if (!clip.GetData(data, 0)) return;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[音频裁剪器] 读取采样失败：" + e.Message);
            return;
        }

        samples = data;

        int channels = Mathf.Max(1, clip.channels);
        int frames = samples.Length / channels;

        mono = new float[frames];

        for (int i = 0; i < frames; i++)
        {
            float sum = 0f;

            for (int c = 0; c < channels; c++)
                sum += samples[i * channels + c];

            mono[i] = sum / channels;
        }
    }

    private void FixImportSettings()
    {
        if (clip == null) return;

        string path = AssetDatabase.GetAssetPath(clip);
        AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;

        if (importer == null) return;

        AudioImporterSampleSettings settings =
            importer.defaultSampleSettings;

        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();

        SetClip(AssetDatabase.LoadAssetAtPath<AudioClip>(path));
    }

    // ====================================================================
    // 波形绘制
    // ====================================================================

    private void BuildWaveformTexture(int width, int height)
    {
        DestroyWaveformTexture();

        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);

        waveformTex = new Texture2D(
            width,
            height,
            TextureFormat.RGBA32,
            false);

        waveformTex.hideFlags = HideFlags.HideAndDontSave;
        waveformTex.wrapMode = TextureWrapMode.Clamp;

        Color background = new Color(0.14f, 0.14f, 0.14f, 1f);
        Color waveColor = new Color(0.45f, 0.8f, 1f, 1f);

        Color[] pixels = new Color[width * height];

        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = background;

        int frames = mono != null ? mono.Length : 0;

        for (int x = 0; x < width; x++)
        {
            int s0 = (int)((long)x * frames / width);
            int s1 = (int)((long)(x + 1) * frames / width);

            if (s1 <= s0) s1 = s0 + 1;

            float min = 1f;
            float max = -1f;

            int step = Mathf.Max(1, (s1 - s0) / 512);

            for (int s = s0; s < s1 && s < frames; s += step)
            {
                float v = mono[s];

                if (v < min) min = v;
                if (v > max) max = v;
            }

            if (min > max)
            {
                min = 0f;
                max = 0f;
            }

            int y0 = Mathf.Clamp(
                Mathf.RoundToInt((1f - max) * 0.5f * (height - 1)),
                0,
                height - 1);

            int y1 = Mathf.Clamp(
                Mathf.RoundToInt((1f - min) * 0.5f * (height - 1)),
                0,
                height - 1);

            if (y1 < y0)
            {
                int temp = y0;
                y0 = y1;
                y1 = temp;
            }

            for (int y = y0; y <= y1; y++)
                pixels[y * width + x] = waveColor;
        }

        waveformTex.SetPixels(pixels);
        waveformTex.Apply();

        waveformTexWidth = width;
        waveformTexHeight = height;
    }

    private void DestroyWaveformTexture()
    {
        if (waveformTex != null)
        {
            DestroyImmediate(waveformTex);
            waveformTex = null;
        }

        waveformTexWidth = 0;
        waveformTexHeight = 0;
    }

    private void DrawWaveform(Rect rect)
    {
        if (Event.current.type != EventType.Repaint) return;

        EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 1f));

        float x0 = TimeToX(startTime, rect);
        float x1 = TimeToX(endTime, rect);

        // 选区外压暗
        EditorGUI.DrawRect(
            new Rect(rect.x, rect.y, Mathf.Max(0f, x0 - rect.x), rect.height),
            new Color(0f, 0f, 0f, 0.4f));

        EditorGUI.DrawRect(
            new Rect(x1, rect.y, Mathf.Max(0f, rect.xMax - x1), rect.height),
            new Color(0f, 0f, 0f, 0.4f));

        int width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
        int height = Mathf.Max(1, Mathf.RoundToInt(rect.height));

        if (waveformTex == null ||
            waveformTexWidth != width ||
            waveformTexHeight != height)
        {
            BuildWaveformTexture(width, height);
        }

        if (waveformTex != null)
            GUI.DrawTexture(rect, waveformTex, ScaleMode.StretchToFill);

        // 两条选区线
        Color lineColor = new Color(1f, 0.72f, 0.2f, 1f);

        EditorGUI.DrawRect(
            new Rect(x0 - 1f, rect.y, 2f, rect.height),
            lineColor);

        EditorGUI.DrawRect(
            new Rect(x1 - 1f, rect.y, 2f, rect.height),
            lineColor);
    }

    private void HandleWaveformInput(Rect rect)
    {
        Event e = Event.current;

        if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
        {
            float x0 = TimeToX(startTime, rect);
            float x1 = TimeToX(endTime, rect);

            float d0 = Mathf.Abs(e.mousePosition.x - x0);
            float d1 = Mathf.Abs(e.mousePosition.x - x1);

            draggingStart = d0 <= d1;
            draggingEnd = !draggingStart;

            e.Use();
            Repaint();
        }
        else if (e.type == EventType.MouseDrag &&
                 (draggingStart || draggingEnd))
        {
            float t = XToTime(e.mousePosition.x, rect);

            if (draggingStart)
                startTime = Mathf.Clamp(t, 0f, endTime - 0.02f);
            else
                endTime = Mathf.Clamp(t, startTime + 0.02f, clip.length);

            e.Use();
            Repaint();
        }
        else if (e.type == EventType.MouseUp)
        {
            draggingStart = false;
            draggingEnd = false;
        }
    }

    private float TimeToX(float time, Rect rect)
    {
        float length = Mathf.Max(0.0001f, clip.length);

        return rect.x +
               Mathf.Clamp01(time / length) * rect.width;
    }

    private float XToTime(float x, Rect rect)
    {
        if (clip == null) return 0f;

        return Mathf.Clamp01((x - rect.x) / Mathf.Max(1f, rect.width)) *
               clip.length;
    }

    // ====================================================================
    // 试听
    // ====================================================================

    private void StartPreview()
    {
        if (clip == null || samples == null) return;

        if (!EnsureAudioUtil())
        {
            EditorUtility.DisplayDialog(
                "无法试听",
                "当前 Unity 版本不支持编辑器内试听，请先导出 WAV 后直接播放。",
                "好");

            return;
        }

        StopPreview();

        int startSample = Mathf.Clamp(
            Mathf.RoundToInt(startTime * clip.frequency),
            0,
            Mathf.Max(0, clip.samples - 1));

        try
        {
            playPreviewClipMethod.Invoke(
                null,
                new object[] { clip, startSample, false });
        }
        catch (Exception e)
        {
            Debug.LogWarning("[音频裁剪器] 试听失败：" + e.Message);
            return;
        }

        previewBeginTime = EditorApplication.timeSinceStartup;
        previewing = true;

        Repaint();
    }

    private void StopPreview()
    {
        if (!previewing) return;

        previewing = false;

        if (EnsureAudioUtil() && stopAllPreviewClipsMethod != null)
        {
            try
            {
                stopAllPreviewClipsMethod.Invoke(null, null);
            }
            catch
            {
                // 忽略
            }
        }

        Repaint();
    }

    private void OnEditorUpdate()
    {
        if (!previewing) return;

        double length = Mathf.Max(0.02f, endTime - startTime);

        if (EditorApplication.timeSinceStartup - previewBeginTime >= length)
            StopPreview();
    }

    private static bool EnsureAudioUtil()
    {
        if (audioUtilInit) return playPreviewClipMethod != null;

        audioUtilInit = true;

        audioUtilType = typeof(Editor).Assembly.GetType(
            "UnityEditor.AudioUtil");

        if (audioUtilType == null) return false;

        BindingFlags flags =
            BindingFlags.Static |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        playPreviewClipMethod = audioUtilType.GetMethod(
            "PlayPreviewClip",
            flags,
            null,
            new[] { typeof(AudioClip), typeof(int), typeof(bool) },
            null);

        stopAllPreviewClipsMethod = audioUtilType.GetMethod(
            "StopAllPreviewClips",
            flags);

        if (stopAllPreviewClipsMethod == null)
        {
            stopAllPreviewClipsMethod = audioUtilType.GetMethod(
                "StopAllClips",
                flags);
        }

        return playPreviewClipMethod != null;
    }

    // ====================================================================
    // 导出
    // ====================================================================

    private void Export()
    {
        if (clip == null || samples == null) return;

        if (endTime - startTime < 0.01f)
        {
            EditorUtility.DisplayDialog(
                "选区太短",
                "请先选择至少 0.01 秒的片段。",
                "好");

            return;
        }

        int channels = Mathf.Max(1, clip.channels);
        int sampleRate = Mathf.Max(1, clip.frequency);

        int startFrame = Mathf.Clamp(
            Mathf.RoundToInt(startTime * sampleRate),
            0,
            Mathf.Max(0, clip.samples - 1));

        int endFrame = Mathf.Clamp(
            Mathf.RoundToInt(endTime * sampleRate),
            startFrame + 1,
            clip.samples);

        int frameCount = endFrame - startFrame;

        if (frameCount <= 0) return;

        string folder = "Assets/Music";

        if (!AssetDatabase.IsValidFolder(folder))
            AssetDatabase.CreateFolder("Assets", "Music");

        string baseName = string.IsNullOrEmpty(outputName)
            ? clip.name + "_trim"
            : outputName.Trim();

        string path = AssetDatabase.GenerateUniqueAssetPath(
            folder + "/" + baseName + ".wav");

        try
        {
            WriteWav(
                path,
                samples,
                startFrame * channels,
                frameCount * channels,
                channels,
                sampleRate);
        }
        catch (Exception e)
        {
            EditorUtility.DisplayDialog(
                "导出失败",
                e.Message,
                "好");

            return;
        }

        AssetDatabase.ImportAsset(path);

        AudioClip output =
            AssetDatabase.LoadAssetAtPath<AudioClip>(path);

        EditorGUIUtility.PingObject(output);
        Selection.activeObject = output;

        Debug.Log("[音频裁剪器] 已导出：" + path);
    }

    private static void WriteWav(
        string path,
        float[] data,
        int offset,
        int count,
        int channels,
        int sampleRate)
    {
        using (FileStream stream = new FileStream(
            path,
            FileMode.Create,
            FileAccess.Write))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            int byteCount = count * 2;

            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + byteCount);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);

            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(byteCount);

            for (int i = 0; i < count; i++)
            {
                float value = Mathf.Clamp(
                    data[offset + i],
                    -1f,
                    1f);

                writer.Write((short)Mathf.RoundToInt(value * 32767f));
            }
        }
    }
}
