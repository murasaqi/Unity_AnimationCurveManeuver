using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace iridescent.AnimationCurveManeuver
{


    [Serializable]
    
    public struct AnimationCurveInfo
    {
        public AnimationCurve curve;
        public EditorCurveBinding binding;
        public string propertyName;
    }
    // public struct ExecutePointInfo
    // {
    //     public float time;
    //     public List<AnimationCurveInfo> AnimationCurveInfos;
    // }
    public class AnimationTrackBaker:EditorWindow
    {
        [MenuItem("Tools/AnimationTrackBaker")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(AnimationTrackBaker));
        }

        public void OnEnable()
        {

            var root = rootVisualElement;

            var objectfield = new ObjectField("PlayableDirector");
            objectfield.objectType = typeof(PlayableDirector);
            
            var executeInfoWrapper = new VisualElement();
            
            var fetchButton = new Button(() =>
            {
                var playableDirector = objectfield.value as PlayableDirector;
                if (playableDirector == null)
                {
                    Debug.LogError("No TimelineAsset");
                    return;
                }
                var timelineAsset = playableDirector.playableAsset as TimelineAsset;

                var tracks = timelineAsset.GetOutputTracks();

                foreach (var track in tracks)
                {

                    if (track.GetType() == typeof(AnimationTrack))
                    {
                     
                        BakeAnimationClipFromAnimationTrack(playableDirector,track as AnimationTrack);   
                    }      
                }
              
                executeInfoWrapper.Clear();

                foreach (var e in executeInfo)
                {
                    
                    var container = new VisualElement();
                    var label = new Label(e.Key.ToString());
                    var label2 = new Label();
                    
                    foreach (var s in e.Value)
                    {
                        label2.text += s + " ";
                    }
                    container.Add(label);
                    container.Add(label2);
                    executeInfoWrapper.Add(container);
                }
            });
            
            
            root.Add(objectfield);
            
            root.Add(fetchButton);
            
            root.Add(executeInfoWrapper);
        }
        
        
        private Dictionary<float,List<AnimationCurveInfo>> executeInfo = new Dictionary<float, List<AnimationCurveInfo>>();

        

        private void BakeAnimationClipFromAnimationTrack(PlayableDirector playableDirector,AnimationTrack track)
        {
            executeInfo.Clear();
            
           
            
            var trackBinding = playableDirector.GetGenericBinding(track) as GameObject;
            
            var timelineClips = track.GetClips();
            foreach (var timelineClip in timelineClips)
            {
                
                var exportClip = new AnimationClip();
                // track.timelineAsset.editorSettings.
                exportClip.name = track.name;
                exportClip.frameRate = (float)track.timelineAsset.editorSettings.frameRate;

                
                var curveBindingDict = new Dictionary<EditorCurveBinding,AnimationCurve>();
                var animationPlayableAsset = timelineClip.asset as AnimationPlayableAsset;

                var animationClip = animationPlayableAsset.clip;
                if (animationClip == null)
                {
                    continue;
                }

                var bindings = AnimationUtility.GetCurveBindings(animationClip);
                foreach (var binding in bindings)
                {
                    var curve = AnimationUtility.GetEditorCurve(animationClip, binding);
                    var copyBinding = new EditorCurveBinding()
                    {
                        path = binding.path,
                        propertyName = binding.propertyName,
                        type = binding.type,
                    };
                    
                    foreach (var key in curve.keys)
                    {

                        if (executeInfo.ContainsKey(key.time))
                        {
                            executeInfo[key.time].Add(new AnimationCurveInfo()
                            {
                                curve = curve,
                                binding = copyBinding,
                                propertyName = copyBinding.propertyName
                            });
                        }
                        else
                        {
                            executeInfo.Add(key.time, new List<AnimationCurveInfo>()
                            {
                                new AnimationCurveInfo()
                                {
                                    curve = curve,
                                    binding = copyBinding,
                                    propertyName = copyBinding.propertyName
                                }
                            });
                        }
                        
                    }
                    curveBindingDict.Add( copyBinding,new AnimationCurve(new Keyframe[]{}));
                }
                
                foreach (var e in executeInfo)
                {
                    playableDirector.time = e.Key;
                    playableDirector.Evaluate();
                
                    foreach (var bindCurvePair in curveBindingDict)
                    {
                        var bind = bindCurvePair.Key;
                        var curve =bindCurvePair.Value;
                        Debug.Log(curve);
                        if(bind.propertyName == "m_LocalPosition.x")
                            curve.AddKey(e.Key,trackBinding.transform.localPosition.x);
                        if(bind.propertyName == "m_LocalPosition.y")
                            curve.AddKey(e.Key,trackBinding.transform.localPosition.y);
                        if(bind.propertyName == "m_LocalPosition.z")
                            curve.AddKey(e.Key,trackBinding.transform.localPosition.z);
                        if(bind.propertyName == "m_LocalRotation.x")
                            curve.AddKey(e.Key,trackBinding.transform.localRotation.x);
                        if(bind.propertyName == "m_LocalRotation.y")
                            curve.AddKey(e.Key,trackBinding.transform.localRotation.y);
                        if(bind.propertyName == "m_LocalRotation.z")
                            curve.AddKey(e.Key,trackBinding.transform.localRotation.z);
                    
                    }
                }
            
                AssetDatabase.CreateAsset(exportClip, "Assets/" + exportClip.name + ".anim");
                AnimationUtility.SetEditorCurves(exportClip,curveBindingDict.Keys.ToArray(),curveBindingDict.Values.ToArray());
                AssetDatabase.SaveAssets();
            }

           
            
        }

    }
}