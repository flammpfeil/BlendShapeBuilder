using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using UTJ.VertexTweaker;
using UTJ.BlendShapeBuilder;
using System.Linq;


namespace UTJ.BlendShapeBuilderEditor
{
    public class BlendShapeInspectorWindow : EditorWindow
    {
        #region fields
        public static bool isOpen;

        Vector2 m_scrollPos;
        UnityEngine.Object m_active;

        static readonly int indentSize = 18;

        string[] names;
        float scrollValue = EditorGUIUtility.singleLineHeight * 3;

        #endregion



        #region callbacks

        [MenuItem("Window/Blend Shape Inspector")]
        public static void Open()
        {
            var window = EditorWindow.GetWindow<BlendShapeInspectorWindow>();
            window.titleContent = new GUIContent("BS Inspector");
            window.Show();
            window.OnSelectionChange();
        }


        private void OnEnable()
        {
            isOpen = true;
        }

        private void OnDisable()
        {
            isOpen = false;
        }

        private void OnFocus()
        {
            OnSelectionChange();
        }

        private void OnGUI()
        {
            m_scrollPos = EditorGUILayout.BeginScrollView(m_scrollPos);
            GUILayout.BeginVertical();
            DrawBlendShapeInspector();
            GUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void OnSelectionChange()
        {
            m_active = null;

            var activeGameObject = Selection.activeGameObject;
            if (activeGameObject != null)
                m_active = activeGameObject;
            else
                m_active = Selection.activeObject;

            if (m_active != null)
            {
                var targetObject = m_active;
                updateNames(Utils.GetMesh(m_active));
            }

            Repaint();
        }

        void updateNames(Mesh targetMesh)
        {
            if (targetMesh != null)
            {
                var materials = Utils.GetMaterials(m_active);

                int numShapes = targetMesh.blendShapeCount;

                names = new string[numShapes];
                for (int i = 0; i < numShapes; i++)
                {
                    names[i] = targetMesh.GetBlendShapeName(i);
                }
            }
        }

        #endregion


        #region impl

        public void DrawBlendShapeInspector()
        {
            var targetObject = m_active;
            var targetMesh = Utils.GetMesh(m_active);
            if (targetMesh == null) { return; }
            var materials = Utils.GetMaterials(m_active);

            EditorGUILayout.ObjectField(targetMesh, typeof(Mesh), true);

            int numShapes = targetMesh.blendShapeCount;
            if (numShapes == 0)
            {
                EditorGUILayout.LabelField("Has no BlendShape");
            }
            else
            {
                EditorGUILayout.LabelField("Has " + numShapes + " Blendshapes");

                GUILayout.BeginHorizontal();
                GUILayout.Space(indentSize);
                GUILayout.BeginVertical();


                for (int si = 0; si < numShapes; ++si)
                {
                    var name = targetMesh.GetBlendShapeName(si);
                    int numFrames = targetMesh.GetBlendShapeFrameCount(si);

                    GUILayout.BeginVertical("Box");
                    GUILayout.BeginHorizontal();

                    GUILayout.Label("" + si, GUILayout.Width(20));

                    {
                        var rect = EditorGUILayout.GetControlRect();
                        var width = rect.width;
                        var pos = rect.position;



                        GUIStyle style = new GUIStyle(EditorStyles.textField);
                        if(name != names[si])
                        {
                            style.normal.textColor = Color.red;    // 通常時の色
                            style.fontStyle = FontStyle.Bold;
                        }

                        names[si] = EditorGUI.TextField(new Rect(pos, new Vector2(width, 16)), names[si], style);

                        if (GUILayout.Button("Update", GUILayout.Width(60)))
                        {
                            UpdateName(targetMesh, si, names[si]);
                        }
                    }


                    GUILayout.Label(" (" + numFrames + " frames)");
                    if (GUILayout.Button("Extract All", GUILayout.Width(90)))
                        ExtractBlendShapeFrames(targetMesh, si, -1, materials);


                    if (si != 0) {
                        if (GUILayout.Button("▲", GUILayout.Width(30)))
                        {
                            ShiftIndex(targetMesh, si, -1);
                            updateNames(targetMesh);
                            this.m_scrollPos.y -= scrollValue;
                        }
                    }else
                        GUILayout.Label("△", GUILayout.Width(30));

                    if (si < (numShapes - 1))
                    {
                        if (GUILayout.Button("▼", GUILayout.Width(30)))
                        {
                            ShiftIndex(targetMesh, si, 1);
                            updateNames(targetMesh);
                            this.m_scrollPos.y += scrollValue;
                        }
                    }
                    else
                        GUILayout.Label("▽", GUILayout.Width(30));

                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Space(indentSize);
                    GUILayout.BeginVertical();
                    for (int fi = 0; fi < numFrames; ++fi)
                    {
                        float weight = targetMesh.GetBlendShapeFrameWeight(si, fi);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label(weight.ToString(), GUILayout.Width(30));
                        if (GUILayout.Button("Extract", GUILayout.Width(60)))
                            ExtractBlendShapeFrames(targetMesh, si, fi, materials);
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.EndVertical();
                    GUILayout.EndHorizontal();

                    GUILayout.EndVertical();
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();

                GUILayout.Space(6);
                if (GUILayout.Button("Convert To Compose Data", GUILayout.Width(200)))
                    ConvertToComposeData(targetObject);
            }
        }


        public static GameObject[] UpdateName(Mesh target, int targetShapeIndex, string newName)
        {


            var blendShapes = Enumerable.Range(0, target.blendShapeCount).Select(si =>
            {
                var name = target.GetBlendShapeName(si);


                //mainlogic UpdateName
                if (targetShapeIndex == si)
                {
                    name = newName;
                }

                var frameCount = target.GetBlendShapeFrameCount(si);


                //Debug.Log("shapeIndex=" + si + " name=" + name + " frames=" + frameCount);

                return new
                {
                    shapeIndex = si,
                    name = name,
                    frames = Enumerable.Range(0, frameCount).Select(fi =>
                    {
                        var dVer = new Vector3[target.vertexCount];
                        var dNol = new Vector3[target.vertexCount];
                        var dTan = new Vector3[target.vertexCount];
                        //Debug.Log("frameIndex=" + fi);
                        target.GetBlendShapeFrameVertices(si, fi, dVer, dNol, dTan);
                        return new
                        {
                            frameIndex = fi,
                            weight = target.GetBlendShapeFrameWeight(si, fi),
                            deltaVertex = dVer,
                            deltaNormal = dNol,
                            deltaTangent = dTan
                        };

                    }).ToArray()
                };
            }).ToArray();


            target.ClearBlendShapes();


            foreach (var blendShape in blendShapes)
            {
                foreach (var frame in blendShape.frames)
                {
                    target.AddBlendShapeFrame(blendShape.name, frame.weight, frame.deltaVertex, frame.deltaNormal, frame.deltaTangent);
                }
            }

            return null;
        }

        // frameIndex = -1: extract all frames
        public static GameObject[] ShiftIndex(Mesh target, int targetShapeIndex, int shift = 0)
        {
            var blendShapes = Enumerable.Range(0, target.blendShapeCount).Select(si =>
            {
                var name = target.GetBlendShapeName(si);
                var frameCount = target.GetBlendShapeFrameCount(si);


                //Debug.Log("shapeIndex=" + si + " name=" + name + " frames=" + frameCount);

                return new
                {
                    shapeIndex = si,
                    name = name,
                    frames = Enumerable.Range(0, frameCount).Select(fi =>
                        {
                            var dVer = new Vector3[target.vertexCount];
                            var dNol = new Vector3[target.vertexCount];
                            var dTan = new Vector3[target.vertexCount];
                            //Debug.Log("frameIndex=" + fi);
                            target.GetBlendShapeFrameVertices(si, fi, dVer, dNol, dTan);
                            return new
                            {
                                frameIndex = fi,
                                weight = target.GetBlendShapeFrameWeight(si, fi),
                                deltaVertex = dVer,
                                deltaNormal = dNol,
                                deltaTangent = dTan
                            };

                        }).ToArray()
                };
            }).ToArray();

            
            target.ClearBlendShapes();


            
            //mainlogic swapIndex
            {

                var tmp = blendShapes[targetShapeIndex + shift];
                blendShapes[targetShapeIndex + shift] = blendShapes[targetShapeIndex];
                blendShapes[targetShapeIndex] = tmp;
            }




            foreach(var blendShape in blendShapes)
            {
                foreach(var frame in blendShape.frames)
                {
                    target.AddBlendShapeFrame(blendShape.name, frame.weight, frame.deltaVertex, frame.deltaNormal, frame.deltaTangent);
                }
            }

            return null;
        }

        // frameIndex = -1: extract all frames
        public static GameObject[] ExtractBlendShapeFrames(Mesh target, int shapeIndex, int frameIndex = -1, Material[] materials = null)
        {
            var name = target.GetBlendShapeName(shapeIndex);
            var numFrames = target.GetBlendShapeFrameCount(shapeIndex);

            var tmpVertices = new Vector3[target.vertexCount];
            var tmpNormals = new Vector3[target.vertexCount];
            var tmpTangents = new Vector4[target.vertexCount];

            var deltaVertices = new Vector3[target.vertexCount];
            var deltaNormals = new Vector3[target.vertexCount];
            var deltaTangents = new Vector3[target.vertexCount];

            var stripped = Instantiate(target);
            stripped.ClearBlendShapes();

            var width = target.bounds.extents.x * 2.0f;
            Func<Mesh, int, GameObject> body = (mesh, fi) =>
            {
                mesh.GetBlendShapeFrameVertices(shapeIndex, fi, deltaVertices, deltaNormals, deltaTangents);
                ApplyDelta(mesh.vertices, deltaVertices, tmpVertices);
                ApplyDelta(mesh.normals, deltaNormals, tmpNormals);
                ApplyDelta(mesh.tangents, deltaTangents, tmpTangents);

                var imesh = Instantiate(stripped);
                imesh.vertices = tmpVertices;
                imesh.normals = tmpNormals;
                imesh.tangents = tmpTangents;
                imesh.name = target.name + ":" + name + "[" + fi + "]";
                var pos = new Vector3(width * (fi + 1), 0.0f, 0.0f);
                var go = Utils.MeshToGameObject(imesh, pos, materials);
                Undo.RegisterCreatedObjectUndo(go, "BlendShapeBuilder");
                return go;
            };

            if (frameIndex < 0)
            {
                var ret = new GameObject[numFrames];
                for (int fi = 0; fi < numFrames; ++fi)
                    ret[fi] = body(target, fi);
                return ret;
            }
            else if(frameIndex < numFrames)
            {
                return new GameObject[1] { body(target, frameIndex) };
            }
            else
            {
                Debug.LogError("Invalid frame index");
                return null;
            }
        }

        private static void ApplyDelta(Vector3[] from, Vector3[] delta, Vector3[] dst)
        {
            var len = from.Length;
            for (int i = 0; i < len; ++i)
                dst[i] = from[i] + delta[i];
        }

        private static void ApplyDelta(Vector4[] from, Vector3[] delta, Vector4[] dst)
        {
            var len = from.Length;
            for (int i = 0; i < len; ++i)
            {
                dst[i].x = from[i].x + delta[i].x;
                dst[i].y = from[i].y + delta[i].y;
                dst[i].z = from[i].z + delta[i].z;
                dst[i].w = from[i].w;
            }
        }


        public static void ConvertToComposeData(UnityEngine.Object targetObject)
        {
            var targetMesh = Utils.GetMesh(targetObject);
            if (targetMesh == null) { return; }
            var materials = Utils.GetMaterials(targetObject);

            int numBS = targetMesh.blendShapeCount;
            if(numBS == 0)
            {
                Debug.Log("BlendShapeInspector: This mesh has no BlendShape.");
                return;
            }

            var baseMesh = Instantiate(targetMesh);
            baseMesh.name = targetMesh.name;
            baseMesh.ClearBlendShapes();
            var baseGO = Utils.MeshToGameObject(baseMesh, Vector3.zero, materials);

            var builder = baseGO.AddComponent<UTJ.BlendShapeBuilder.BlendShapeBuilder>();
            var data = builder.data.blendShapeData;
            data.Clear();

            for (int bi = 0; bi < numBS; ++bi)
            {
                var name = targetMesh.GetBlendShapeName(bi);
                int numFrames = targetMesh.GetBlendShapeFrameCount(bi);
                var gos = ExtractBlendShapeFrames(targetMesh, bi, -1, materials);

                float step = 100.0f / numFrames;
                float weight = step;
                var bsd = new BlendShapeData();
                data.Add(bsd);
                bsd.name = name;
                foreach(var go in gos)
                {
                    var fd = new BlendShapeFrameData();
                    bsd.frames.Add(fd);
                    fd.mesh = go;
                    fd.weight = weight;
                    weight += step;
                }
            }
            Undo.RegisterCreatedObjectUndo(baseGO, "BlendShapeBuilder");

            Selection.activeObject = baseGO;
            BlendShapeBuilderWindow.Open();
        }

        #endregion

    }
}
