using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
//using YamlDotNet.Core.Tokens;

namespace iridescent.util
{
    public static class TimelineUtil
    {
        // Start is called before the first frame update
        public static float GetFrameRate(PlayableDirector playableDirector)
        {
            var timelineAsset = playableDirector.playableAsset as TimelineAsset;
            return (float) timelineAsset.editorSettings.frameRate;
        }
        
        
        public static int GetFrameCount(double time, PlayableDirector playableDirector)
        {
            var timelineAsset = playableDirector.playableAsset as TimelineAsset;
            var fps = (float) timelineAsset.editorSettings.frameRate;
            return Mathf.CeilToInt(fps * (float) time);
        }

        public static float DurationByFrame(PlayableDirector playableDirector)
        {
            return 1f / GetFrameRate(playableDirector);
        }

        public static void AddKeyAtStartEndTime(AnimationClip clip, float start, float end)
        {
            
        }

        public static Dictionary<string, AnimationTrack> GetAnimationTrackNameList(TimelineAsset timelineAsset)
        {
            var list = new Dictionary<string, AnimationTrack>();
            var tracks = timelineAsset.GetOutputTracks();
            var trackIndex = 0;
            foreach (var track in tracks)
            {
                if (track.GetType() == typeof(AnimationTrack))
                {
                    list.Add($"{trackIndex}:{track.name}",track as AnimationTrack);
                }

                trackIndex++;
            }

            return list;
        }


        public static float SnapTime(float frameRate, float time)
        {
            var frame = Mathf.CeilToInt(time * frameRate);
            return frame / frameRate;
        }
        

        public static Vector2 OverWrapRange(double startA, double endA, double startB, double endB)
        {
            var a = (float)startA;
            var b = (float)endA;
            var c = (float)startB;
            var d = (float)endB;

            return new Vector2(Mathf.Max(a, c),Mathf.Min(b, d));

        }

        public static bool IsTimelineClipOverWrap(TimelineClip A, TimelineClip B)
        {
            var overWrap = OverWrapRange(A.start, A.end, B.start, B.end);

            return overWrap.y < overWrap.x;
        }

        public static Vector2 GetTimelineClipStartEndOverWrap(float baseStart, float baseEnd, float overStart,float overEnd)
        {

            var start = -1f;
            var end = -1f;
            var a = baseStart;
            var b = baseEnd;
            var c = overStart;
            var d = overEnd;

            start = Mathf.Max(a, c);
            end = Mathf.Min(b, d);

            if (end < start)
            {
                return new Vector2(-1f,-1f);
            }
            else
            {
                return new Vector2(start, end);
            }

        }
        
        /*

        public static Dictionary<EditorCurveBinding, AnimationCurve> ConvertKeyTimeToTimelineTime(TimelineClip clip, RotationBakeType rotationBakeType)
        {
            var result = AnimationManeuver.GetEditorBindingCurveKeyPairViaYaml(clip.animationClip,rotationBakeType);
            result = ConvertKeyTimeToTimelineTime(clip, result);
            return result;
        }
       

        // keyFrameのタイムをTimelineのAbsoluteTimeに変換する 
        public static Dictionary<EditorCurveBinding,AnimationCurve> ConvertKeyTimeToTimelineTime(TimelineClip timelineClip, Dictionary<EditorCurveBinding,AnimationCurve> editorCurveBindingAnimationCurvePair)
        {
            // Debug.Log($"slipStart:{timelineClip.start}, clipEnd:{timelineClip.end},clipIndex:{timelineClip.clipIn}");
            var diff = (float)timelineClip.start - (float)timelineClip.clipIn;

            Debug.Log($"clip:{timelineClip.displayName}: diff:{diff}, clipStart:{timelineClip.start}, clipEnd:{timelineClip.end}, clipIn:{timelineClip.clipIn}");
            if (diff < 0)
            {
                foreach (var pair in editorCurveBindingAnimationCurvePair)
                {
                    CurveUtilityWrapper.InjectKeyAtTime(pair.Value,(float)Math.Abs(diff));    
                }
            }
          
            var newEditorCurveBindingAnimationCurvePair = new Dictionary<EditorCurveBinding, AnimationCurve>();
            foreach (var pair in editorCurveBindingAnimationCurvePair)
            {
                var newCurve = new AnimationCurve();
                var keyIndex = 0;
                foreach (var key in pair.Value.keys)
                {
                    var offsetTime = key.time + diff;
                    if (offsetTime < 0) continue;
                    
                    var offsetKey = new Keyframe()
                    {
                        time = (float)offsetTime,
                        value = key.value,
                        inTangent = key.inTangent,
                        outTangent = key.outTangent,
                        inWeight =  key.inWeight,
                        outWeight = key.outWeight,
                    };
                    newCurve.AddKey(offsetKey);
                    AnimationUtility.SetKeyLeftTangentMode(newCurve,newCurve.keys.Length-1,
                        AnimationUtility.GetKeyLeftTangentMode(pair.Value, keyIndex));
                    AnimationUtility.SetKeyRightTangentMode(newCurve,newCurve.keys.Length-1,
                        AnimationUtility.GetKeyRightTangentMode(pair.Value, keyIndex));


                }
                
                newEditorCurveBindingAnimationCurvePair.Add(pair.Key,newCurve);
            }

            return newEditorCurveBindingAnimationCurvePair;
        }
        */

        public static double GetOffsetKeyTime(TimelineClip clip, double curveKeyTime)
        {
            return curveKeyTime - clip.start + clip.clipIn;
        }

        public static float GetOffsetKeyTime(TimelineClip clip, float curveKeyTime)
        {
            return curveKeyTime - (float)clip.start + (float)clip.clipIn;
        }


        // public static void AddKeyAtAnimationPlayableAssetStartEnd()
        // {
        //     
        // }
        
        public static DirectoryInfo SafeCreateDirectory(string path)
        {
            if (Directory.Exists(path))
            {
                return null;
            }

            return Directory.CreateDirectory(path);
        }
        
        public static void CreateAnimationClipAssetWithOverwrite(UnityEngine.Object asset, string exportPath)
        {
            SafeCreateDirectory(Path.GetDirectoryName(exportPath));

            //アセットが存在しない場合はそのまま作成(metaファイルも新規作成)
            if (!File.Exists(exportPath))
            {
                AssetDatabase.CreateAsset(asset, exportPath);
                return;
            }
            //既存のファイルがあればUndo登録する
            else
            {
                var existingClip = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(exportPath);
                Undo.RegisterCompleteObjectUndo(existingClip, existingClip.name);
            }

            //仮ファイルを作るためのディレクトリを作成
            var fileName = Path.GetFileName(exportPath);
            var tmpDirectoryPath = Path.Combine(exportPath.Replace(fileName, ""), "tmpDirectory");
            Directory.CreateDirectory(tmpDirectoryPath);

            //仮ファイルを保存
            var tmpFilePath = Path.Combine(tmpDirectoryPath, fileName);
            AssetDatabase.CreateAsset(asset, tmpFilePath);

            //仮ファイルを既存のファイルに上書き(metaデータはそのまま)
            FileUtil.ReplaceFile(tmpFilePath, exportPath);

            //仮ディレクトリとファイルを削除
            AssetDatabase.DeleteAsset(tmpDirectoryPath);

            //データ変更をUnityに伝えるためインポートしなおし
            AssetDatabase.ImportAsset(exportPath);
        }
    }
}