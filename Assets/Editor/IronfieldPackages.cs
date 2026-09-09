using System;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Ironfield.EditorTools
{
    /// <summary>
    /// Phase A of headless setup: add the packages the game needs. This file
    /// deliberately references nothing from those packages so it always compiles.
    /// Run once with:
    ///   Unity -batchmode -quit -executeMethod Ironfield.EditorTools.IronfieldPackages.Run
    /// After it finishes the editor recompiles with URP / Input System / Cinemachine
    /// available, and IronfieldSetup can run.
    /// </summary>
    public static class IronfieldPackages
    {
        static readonly string[] Wanted =
        {
            "com.unity.render-pipelines.universal",
            "com.unity.inputsystem",
            "com.unity.cinemachine",
            "com.unity.ugui",
            "com.unity.test-framework",
        };

        [MenuItem("Ironfield/1. Add Packages")]
        public static void Run()
        {
            foreach (var id in Wanted)
            {
                Debug.Log($"[Ironfield] Adding package {id} ...");
                AddBlocking(id);
            }
            Debug.Log("[Ironfield] Package phase complete.");
        }

        static void AddBlocking(string id)
        {
            AddRequest req = Client.Add(id);
            while (!req.IsCompleted)
                System.Threading.Thread.Sleep(100);

            if (req.Status == StatusCode.Success)
                Debug.Log($"[Ironfield]   ok: {req.Result.packageId}");
            else
                Debug.LogError($"[Ironfield]   FAILED {id}: {req.Error?.message}");
        }
    }
}
