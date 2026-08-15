using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace GameEditor.ImageToMesh
{
    public sealed class MeshVertexDeltaEncoderWindow : EditorWindow
    {
        private const int MaxKeyFrameCount = 12;

        [SerializeField]
        private Mesh startMesh;

        [SerializeField]
        private List<Mesh> targetMeshes = new List<Mesh>();

        [MenuItem("Tools/Mesh/Encode Vertex Key Frames")]
        private static void Open()
        {
            GetWindow<MeshVertexDeltaEncoderWindow>("Mesh Key Frames");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space();
            startMesh = (Mesh)EditorGUILayout.ObjectField(
                new GUIContent(
                    "Start Pose Mesh",
                    "Drag one initial pose Mesh here. The output Mesh is copied from this Mesh."),
                startMesh,
                typeof(Mesh),
                false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Animation Pose Meshes", EditorStyles.boldLabel);
            for (int i = 0; i < targetMeshes.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    targetMeshes[i] = (Mesh)EditorGUILayout.ObjectField(
                        new GUIContent($"Key Frame {i + 1}"),
                        targetMeshes[i],
                        typeof(Mesh),
                        false);

                    using (new EditorGUI.DisabledScope(i == 0))
                    {
                        if (GUILayout.Button("Up", GUILayout.Width(36f)))
                        {
                            Mesh previousMesh = targetMeshes[i - 1];
                            targetMeshes[i - 1] = targetMeshes[i];
                            targetMeshes[i] = previousMesh;
                        }
                    }

                    using (new EditorGUI.DisabledScope(i == targetMeshes.Count - 1))
                    {
                        if (GUILayout.Button("Down", GUILayout.Width(42f)))
                        {
                            Mesh nextMesh = targetMeshes[i + 1];
                            targetMeshes[i + 1] = targetMeshes[i];
                            targetMeshes[i] = nextMesh;
                        }
                    }

                    if (GUILayout.Button("Remove", GUILayout.Width(60f)))
                    {
                        targetMeshes.RemoveAt(i);
                        i--;
                    }
                }
            }

            Rect dropArea = GUILayoutUtility.GetRect(0f, 48f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag Multiple Animation Pose Meshes Here");
            Event currentEvent = Event.current;
            if ((currentEvent.type == EventType.DragUpdated ||
                 currentEvent.type == EventType.DragPerform) &&
                dropArea.Contains(currentEvent.mousePosition))
            {
                bool containsMesh = false;
                for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
                {
                    if (DragAndDrop.objectReferences[i] is Mesh)
                    {
                        containsMesh = true;
                        break;
                    }
                }

                if (containsMesh && targetMeshes.Count < MaxKeyFrameCount)
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    if (currentEvent.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        for (int i = 0; i < DragAndDrop.objectReferences.Length; i++)
                        {
                            if (DragAndDrop.objectReferences[i] is Mesh mesh &&
                                targetMeshes.Count < MaxKeyFrameCount)
                            {
                                targetMeshes.Add(mesh);
                            }
                        }
                    }

                    currentEvent.Use();
                }
            }

            using (new EditorGUI.DisabledScope(targetMeshes.Count >= MaxKeyFrameCount))
            {
                if (GUILayout.Button("Add Key Frame"))
                {
                    targetMeshes.Add(null);
                }
            }

            EditorGUILayout.HelpBox(
                "Drag one initial pose into Start Pose Mesh, then drag up to 12 animation pose " +
                "Meshes into the area below. Pose 1 stores its XY vertex delta from Start. Each " +
                "later pose stores its XY delta from the previous pose. The matching Shader " +
                "uses one additional interval to interpolate the final pose back to Start, so " +
                "the initial pose plus 12 animation poses form a 13-pose loop.",
                MessageType.Info);
            EditorGUILayout.LabelField(
                "Total Loop Poses",
                (targetMeshes.Count + 1).ToString());

            EditorGUILayout.Space();
            bool canGenerate = startMesh != null &&
                               targetMeshes.Count > 0 &&
                               targetMeshes.Count <= MaxKeyFrameCount;
            for (int i = 0; i < targetMeshes.Count; i++)
            {
                if (targetMeshes[i] == null)
                {
                    canGenerate = false;
                    break;
                }
            }

            using (new EditorGUI.DisabledScope(canGenerate == false))
            {
                if (GUILayout.Button("Generate Mesh Asset", GUILayout.Height(30f)))
                {
                    Generate();
                }
            }
        }

        private void Generate()
        {
            string outputPath = EditorUtility.SaveFilePanelInProject(
                "Save Vertex Key Frame Mesh",
                $"{startMesh.name}_KeyFrames",
                "asset",
                "Choose where to save the generated Mesh asset.");
            if (string.IsNullOrEmpty(outputPath))
            {
                return;
            }

            try
            {
                Mesh outputMesh = EncodeToAsset(
                    startMesh,
                    targetMeshes,
                    outputPath);
                Selection.activeObject = outputMesh;
                EditorGUIUtility.PingObject(outputMesh);
            }
            catch (System.ArgumentException exception)
            {
                EditorUtility.DisplayDialog(
                    "Mesh Vertex Delta",
                    exception.Message,
                    "OK");
            }
        }

        public static Mesh EncodeToAsset(
            Mesh startPoseMesh,
            IReadOnlyList<Mesh> animationPoseMeshes,
            string outputPath)
        {
            if (startPoseMesh == null)
            {
                throw new System.ArgumentException(
                    "Start Pose Mesh is required.");
            }

            if (animationPoseMeshes == null ||
                animationPoseMeshes.Count == 0 ||
                animationPoseMeshes.Count > MaxKeyFrameCount)
            {
                throw new System.ArgumentException(
                    $"Animation Pose Mesh count must be between 1 and {MaxKeyFrameCount}.");
            }

            for (int i = 0; i < animationPoseMeshes.Count; i++)
            {
                Mesh animationPoseMesh = animationPoseMeshes[i];
                if (animationPoseMesh == null)
                {
                    throw new System.ArgumentException(
                        $"Animation Pose Mesh {i + 1} is required.");
                }

                if (startPoseMesh.vertexCount != animationPoseMesh.vertexCount)
                {
                    throw new System.ArgumentException(
                        $"Vertex counts do not match.\n" +
                        $"Start: {startPoseMesh.vertexCount}\n" +
                        $"Key Frame {i + 1}: {animationPoseMesh.vertexCount}");
                }
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                throw new System.ArgumentException(
                    "Output asset path is required.");
            }

            Vector3[] startVertices = startPoseMesh.vertices;
            Mesh outputMesh = Instantiate(startPoseMesh);
            outputMesh.name = System.IO.Path.GetFileNameWithoutExtension(outputPath);

            Vector3[] normalData = new Vector3[startVertices.Length];
            Vector4[] tangentData = new Vector4[startVertices.Length];
            List<Vector4> uv0Data = new List<Vector4>(startVertices.Length);
            startPoseMesh.GetUVs(0, uv0Data);

            List<Vector4>[] packedUvChannels = new List<Vector4>[4];
            for (int channelIndex = 0;
                 channelIndex < packedUvChannels.Length;
                 channelIndex++)
            {
                packedUvChannels[channelIndex] = new List<Vector4>(startVertices.Length);
                for (int vertexIndex = 0; vertexIndex < startVertices.Length; vertexIndex++)
                {
                    packedUvChannels[channelIndex].Add(Vector4.zero);
                }
            }

            Vector3[] previousVertices = startVertices;
            for (int keyFrameIndex = 0;
                 keyFrameIndex < animationPoseMeshes.Count;
                 keyFrameIndex++)
            {
                // Every animation pose is encoded relative to the previous pose. The Shader
                // reserves its final interval for interpolation from the last pose to Start.
                Vector3[] targetVertices = animationPoseMeshes[keyFrameIndex].vertices;

                for (int vertexIndex = 0; vertexIndex < targetVertices.Length; vertexIndex++)
                {
                    float deltaX = targetVertices[vertexIndex].x - previousVertices[vertexIndex].x;
                    float deltaY = targetVertices[vertexIndex].y - previousVertices[vertexIndex].y;

                    // NORMAL stores pose 1, TANGENT stores poses 2-3, TEXCOORD0.zw stores pose 4,
                    // and TEXCOORD1-4 store poses 5-12 in their xy/zw component pairs.
                    if (keyFrameIndex == 0)
                    {
                        normalData[vertexIndex] = new Vector3(deltaX, deltaY, 0f);
                    }
                    else if (keyFrameIndex == 1)
                    {
                        tangentData[vertexIndex].x = deltaX;
                        tangentData[vertexIndex].y = deltaY;
                    }
                    else if (keyFrameIndex == 2)
                    {
                        tangentData[vertexIndex].z = deltaX;
                        tangentData[vertexIndex].w = deltaY;
                    }
                    else if (keyFrameIndex == 3)
                    {
                        Vector4 uv0 = uv0Data[vertexIndex];
                        uv0.z = deltaX;
                        uv0.w = deltaY;
                        uv0Data[vertexIndex] = uv0;
                    }
                    else
                    {
                        int channelIndex = (keyFrameIndex - 4) / 2;
                        bool useXY = keyFrameIndex % 2 == 0;
                        List<Vector4> packedChannel = packedUvChannels[channelIndex];
                        Vector4 packedDelta = packedChannel[vertexIndex];
                        if (useXY)
                        {
                            packedDelta.x = deltaX;
                            packedDelta.y = deltaY;
                        }
                        else
                        {
                            packedDelta.z = deltaX;
                            packedDelta.w = deltaY;
                        }

                        packedChannel[vertexIndex] = packedDelta;
                    }
                }

                previousVertices = targetVertices;
            }

            outputMesh.normals = normalData;
            outputMesh.tangents = tangentData;
            outputMesh.SetUVs(0, uv0Data);
            for (int channelIndex = 0;
                 channelIndex < packedUvChannels.Length;
                 channelIndex++)
            {
                outputMesh.SetUVs(channelIndex + 1, packedUvChannels[channelIndex]);
            }

            Bounds outputBounds = startPoseMesh.bounds;
            for (int keyFrameIndex = 0;
                 keyFrameIndex < animationPoseMeshes.Count;
                 keyFrameIndex++)
            {
                outputBounds.Encapsulate(
                    animationPoseMeshes[keyFrameIndex].bounds);
            }

            outputMesh.bounds = outputBounds;
            Object existingAsset = AssetDatabase.LoadMainAssetAtPath(outputPath);
            if (existingAsset == null)
            {
                AssetDatabase.CreateAsset(outputMesh, outputPath);
            }
            else if (existingAsset is Mesh existingMesh)
            {
                EditorUtility.CopySerialized(outputMesh, existingMesh);
                DestroyImmediate(outputMesh);
                EditorUtility.SetDirty(existingMesh);
                outputMesh = existingMesh;
            }
            else
            {
                DestroyImmediate(outputMesh);
                throw new System.ArgumentException(
                    $"Output path is occupied by another asset: {outputPath}");
            }

            AssetDatabase.SaveAssets();
            return outputMesh;
        }
    }
}
