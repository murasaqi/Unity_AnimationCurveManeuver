using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
namespace iridescent.AnimationCurveManeuver
{
    public class AnimationCurveUtilityWrapper : MonoBehaviour
    {
        private static Assembly EditorWindowAssembly => typeof(EditorWindow).Assembly;
        private static MethodInfo SettKeyModeFromContextMethodInfo => EditorWindowAssembly.GetType("UnityEditor.CurveUtility").GetMethod("SetKeyModeFromContext", BindingFlags.Static | BindingFlags.NonPublic| BindingFlags.InvokeMethod);
        private static MethodInfo UpdateTangentsFromModeMethodInfo => typeof(AnimationUtility).GetMethod("UpdateTangentsFromMode", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.InvokeMethod);
        private static MethodInfo UpdateTangentsFromModeSurroundingMethodInfo =>  typeof(AnimationUtility).GetMethod("UpdateTangentsFromModeSurrounding", BindingFlags.Static | BindingFlags.NonPublic| BindingFlags.InvokeMethod);
        public static int AddBetweenKey(AnimationCurve curve, float time)
        {
            var addBetweenKey = typeof(AnimationUtility).GetMethod(
                "AddInbetweenKey",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.InvokeMethod);
            return (int) addBetweenKey.Invoke(null, new object[] {curve, time});
        }

        public static void SetKeyModeFromContext(AnimationCurve curve, int keyIndex)
        {
            // var curveUtilityType = EditorWindowAssembly.GetType("UnityEditor.CurveUtility");
            // var setKeyModeFromContext = curveUtilityType.GetMethod("SetKeyModeFromContext", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.InvokeMethod);
            SettKeyModeFromContextMethodInfo.Invoke(null, new object[] {curve, keyIndex});
        }

        public static void UpdateTangentsFromModeSurrounding(AnimationCurve curve, int keyIndex)
        {
            // var updateTangentsFromModeSurrounding =
            //     typeof(AnimationUtility).GetMethod("UpdateTangentsFromModeSurrounding", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.InvokeMethod);
            UpdateTangentsFromModeSurroundingMethodInfo.Invoke(null, new object[] {curve, keyIndex});
        }
            
        public static void UpdateTangentsFromMode(AnimationCurve curve)
        {
            // var updateTangentsFromMode =
            //     typeof(AnimationUtility).GetMethod("UpdateTangentsFromMode", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.InvokeMethod);
            UpdateTangentsFromModeMethodInfo.Invoke(null, new object[] {curve});
        }

    }

}