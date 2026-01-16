using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DreadScripts.AnimationHierarchyEditor
{
	public class AnimationHierarchyEditor : EditorWindow
	{

		#region Automated Variables

		private readonly Dictionary<string, List<EditorCurveBinding>> paths = new Dictionary<string, List<EditorCurveBinding>>();
		private readonly List<string> pathsKeys = new List<string>();

		private static GUIContent resetIcon;
		private AnimationClip[] animationClips;
		private Vector2 scrollPos = Vector2.zero;

		#endregion

		#region Input Variables

		private string[] tempPathOverrides;

		private Animator animatorObject;

		private bool regexReplace;
		private string oldPathValue = "Root";
		private string newPathValue = "SomeNewObject/Root";

		#endregion

		[MenuItem("DreadTools/Utility/Animation Hierarchy Editor")]
		public static void ShowWindow()
		{
			GetWindow<AnimationHierarchyEditor>(false, "Animation Hierarchy Editor", true).titleContent.image = EditorGUIUtility.IconContent("AnimationClip Icon").image;
		}

		public void OnSelectionChange()
		{
			animationClips = Selection.GetFiltered<AnimationClip>(SelectionMode.Assets);
			FillModel();
			Repaint();
		}


		public void OnGUI()
		{
			if (animationClips == null || animationClips.Length == 0)
			{
				DrawTitle("Please select an Animation Clip");
				return;
			}


			scrollPos = GUILayout.BeginScrollView(scrollPos, GUIStyle.none);

			using (new GUILayout.VerticalScope("helpbox"))
			{
				animatorObject = (Animator) EditorGUILayout.ObjectField("Root Animator:", animatorObject, typeof(Animator), true);

				using (new GUILayout.HorizontalScope())
				{
					if (animationClips.Length == 1)
						animationClips[0] = (AnimationClip) EditorGUILayout.ObjectField("Target Clip:", animationClips[0], typeof(AnimationClip), false);
					else
					{
						EditorGUIUtility.labelWidth = 95;
						EditorGUILayout.LabelField("Target Clips:", GUILayout.ExpandWidth(false));
						EditorGUIUtility.labelWidth = 1;

						EditorGUILayout.LabelField($"Mutiple Anim Clips ({animationClips.Length})");
						EditorGUIUtility.labelWidth = 0;
					}
				}
			}


			GUILayout.Space(12);

			using (new GUILayout.VerticalScope("helpbox"))
			{
				using (new EditorGUILayout.HorizontalScope("box"))
				{
					oldPathValue = EditorGUILayout.TextField(oldPathValue);
					newPathValue = EditorGUILayout.TextField(newPathValue);

					using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(oldPathValue)))
						if (GUILayout.Button(new GUIContent("Replace Path", "Replaces the old string (left) with the new string (right). Field on the left will be used as a Regex Pattern if Use Regex (@) is enabled.")))
							UpdatePath(oldPathValue, newPathValue, true);
					regexReplace = GUILayout.Toggle(regexReplace, new GUIContent("@", "Use Regex"), "button", GUILayout.Width(22));
				}

				GUILayout.Space(12);

				DisplayPathItems();
			}

			GUILayout.Space(40);
			GUILayout.EndScrollView();
		}

		public void DisplayPathItems()
		{
			GUIStyle resetButtonStyle = new GUIStyle() {contentOffset = new Vector2(0, 3.5f)};
			for (int i = 0; i < pathsKeys.Count; i++)
			{
				string path = pathsKeys[i];
				GameObject obj = FindObjectInRoot(path);
				List<EditorCurveBinding> properties = paths[path];

				using (new GUILayout.HorizontalScope())
				{
					bool isModifiedPath = tempPathOverrides[i] != path;

					using (new EditorGUI.DisabledScope(!isModifiedPath))
						if (GUILayout.Button(resetIcon, resetButtonStyle, GUILayout.ExpandWidth(false)))
							tempPathOverrides[i] = path;
					tempPathOverrides[i] = EditorGUILayout.TextField(tempPathOverrides[i]);

					using (new EditorGUI.DisabledScope(!isModifiedPath))
						if (GUILayout.Button("Apply", GUILayout.Width(60)))
							UpdatePath(path, tempPathOverrides[i], false);

					GUILayout.Label($"({properties.Count})", GUILayout.Width(40));

					Color standardColor = GUI.color;
					GUI.color = obj != null ? Color.green : Color.red;

					EditorGUI.BeginChangeCheck();
					GameObject newObj = (GameObject) EditorGUILayout.ObjectField(obj, typeof(GameObject), true);
					if (EditorGUI.EndChangeCheck()) UpdatePath(path, ChildPath(newObj), false);

					GUI.color = standardColor;

				}
			}
		}

		/*void OnInspectorUpdate() {
        this.Repaint();
    }*/

		private void FillModel()
		{
			paths.Clear();
			pathsKeys.Clear();

			foreach (AnimationClip animationClip in animationClips)
			{
				FillModelWithCurves(AnimationUtility.GetCurveBindings(animationClip));
				FillModelWithCurves(AnimationUtility.GetObjectReferenceCurveBindings(animationClip));
			}

			tempPathOverrides = pathsKeys.ToArray();
		}

		private void FillModelWithCurves(EditorCurveBinding[] curves)
		{
			foreach (EditorCurveBinding curveData in curves)
			{
				string key = curveData.path;

				if (paths.ContainsKey(key))
				{
					paths[key].Add(curveData);
				}
				else
				{
					paths.Add(key, new List<EditorCurveBinding>() {curveData});
					pathsKeys.Add(key);
				}
			}
		}

		public void UpdatePath(string oldPath, string newPath, bool matchWholeWord)
		{
			if (oldPath == newPath) return;

			bool identicalMayExist = !matchWholeWord && paths.TryGetValue(newPath, out _);

			try
			{
				AssetDatabase.StartAssetEditing();
				for (int clipIndex = 0; clipIndex < animationClips.Length; clipIndex++)
				{
					AnimationClip animationClip = animationClips[clipIndex];
					Undo.RecordObject(animationClip, "Animation Hierarchy Change");

					List<EditorCurveBinding> curves = AnimationUtility.GetCurveBindings(animationClip)
					                                                  .Concat(AnimationUtility.GetObjectReferenceCurveBindings(animationClip)).ToList();

					foreach (var c in curves)
					{
						EditorCurveBinding binding = c;
						AnimationCurve curve = AnimationUtility.GetEditorCurve(animationClip, binding);
						ObjectReferenceKeyframe[] objectReferenceCurve = AnimationUtility.GetObjectReferenceCurve(animationClip, binding);

						bool isFloatCurve = curve != null;

						if (isFloatCurve) AnimationUtility.SetEditorCurve(animationClip, binding, null);
						else AnimationUtility.SetObjectReferenceCurve(animationClip, binding, null);

						if (!matchWholeWord && binding.path == oldPath)
						{
							if (identicalMayExist && curves.Any(c2 => c2.path == newPath && c2.type == c.type && c2.propertyName == c.propertyName))
								Debug.LogWarning($"Identical settings curve already exists. Skipping curve.\nPath: {c.path}\nType: {c.type.Name}\nProperty: {c.propertyName}");
							else binding.path = newPath;
						}
						else if (matchWholeWord)
						{
							binding.path = regexReplace
								? Regex.Replace(binding.path, oldPath, newPath)
								: binding.path.Replace(oldPath, newPath);
						}

						if (isFloatCurve) AnimationUtility.SetEditorCurve(animationClip, binding, curve);
						else AnimationUtility.SetObjectReferenceCurve(animationClip, binding, objectReferenceCurve);
					}

					DisplayProgress(clipIndex);

				}
			}
			finally
			{
				AssetDatabase.StopAssetEditing();
				EditorUtility.ClearProgressBar();
			}

			FillModel();
			Repaint();
		}

		#region Automated Methods

		private void OnFocus()
		{
			OnSelectionChange();
		}

		private void OnEnable()
		{
			Undo.undoRedoPerformed -= FillModel;
			Undo.undoRedoPerformed += FillModel;

			resetIcon = new GUIContent(EditorGUIUtility.IconContent("d_Refresh")) {tooltip = "Reset Dimensions"};
		}

		private void OnDisable()
		{
			Undo.undoRedoPerformed -= FillModel;
		}

		#endregion


		#region Helper Methods

		private GameObject FindObjectInRoot(string path) => animatorObject ? animatorObject.transform.Find(path)?.gameObject : null;

		private string ChildPath(GameObject obj)
		{
			if (animatorObject == null) throw new UnityException("Please assign the Root Animator first!");
			if (!obj.transform.IsChildOf(animatorObject.transform)) throw new UnityException($"Object must belong to {animatorObject.name} !");

			return AnimationUtility.CalculateTransformPath(obj.transform, animatorObject.transform);
		}

		private static void DrawTitle(string title)
		{
			using (new GUILayout.HorizontalScope("in bigtitle"))
				GUILayout.Label(title, new GUIStyle("boldLabel") {alignment = TextAnchor.MiddleCenter});

		}

		private void DisplayProgress(int clipIndex)
		{
			float fChunk = 1f / animationClips.Length;
			float fProgress = fChunk * clipIndex;
			EditorUtility.DisplayProgressBar("Animation Hierarchy Progress", "Editing animations.", fProgress);
		}

		#endregion
	}

}