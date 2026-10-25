using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public class TrainerCardAnimationTests
{
    [TestCase(true)]
    [TestCase(false)]
    public void ConfiguredCardPosesExistOnTheirGenderController(bool male)
    {
        var modelType = Type.GetType("TrainerModel, Assembly-CSharp", true);
        var cardType = Type.GetType("TrainerCardUI, Assembly-CSharp", true);
        var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TrainerModels/TrainerModel.prefab");
        var cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TrainerModels/TrainerCard.prefab");
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var animator = (Animator)modelType.GetField(male ? "maleAnimator" : "femaleAnimator", flags)
            .GetValue(modelPrefab.GetComponent(modelType));
        var controller = (AnimatorController)animator.runtimeAnimatorController;
        var states = controller.layers[0].stateMachine.states.Select(state => state.state.name).ToArray();
        var poses = (string[])cardType.GetField(male ? "maleAnimations" : "femaleAnimations", flags)
            .GetValue(cardPrefab.GetComponentInChildren(cardType, true));
        Assert.That(poses, Is.Not.Empty);
        Assert.That(poses.Where(pose => !states.Contains(pose)), Is.Empty,
            "The fix must play the real configured poses, not suppress missing-state warnings.");
    }
}
