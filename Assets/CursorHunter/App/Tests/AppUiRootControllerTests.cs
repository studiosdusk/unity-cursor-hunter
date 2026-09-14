using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CursorHunter.App.Tests
{
    public sealed class AppUiRootControllerTests
    {
        private readonly List<GameObject> _objects =
            new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int index = _objects.Count - 1; index >= 0; index--)
            {
                if (_objects[index] != null)
                {
                    Object.DestroyImmediate(_objects[index]);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void InitialScreenHidesEveryOtherRegisteredRoot()
        {
            AppUiRootController controller = CreateController();

            Assert.That(controller.CurrentScreen, Is.EqualTo(UiScreenId.MainMenu));
            Assert.That(IsActive("MainMenu"), Is.True);
            Assert.That(IsActive("FieldSelect"), Is.False);
            Assert.That(IsActive("Combat"), Is.False);
        }

        [Test]
        public void ScreenTransitionIsCentralizedAndRepeatedSelectionIsIdempotent()
        {
            AppUiRootController controller = CreateController();
            List<string> transitions = new List<string>();
            controller.ScreenChanged += (from, to) =>
                transitions.Add($"{from}->{to}");

            controller.ShowFieldSelect();
            Assert.That(controller.CurrentScreen, Is.EqualTo(UiScreenId.FieldSelect));
            Assert.That(IsActive("MainMenu"), Is.False);
            Assert.That(IsActive("FieldSelect"), Is.True);
            Assert.That(IsActive("Combat"), Is.False);

            controller.ShowFieldSelect();
            controller.EnterCombat();

            Assert.That(controller.CurrentScreen, Is.EqualTo(UiScreenId.Combat));
            Assert.That(IsActive("FieldSelect"), Is.False);
            Assert.That(IsActive("Combat"), Is.True);
            Assert.That(
                transitions,
                Is.EqualTo(new[]
                {
                    "MainMenu->FieldSelect",
                    "FieldSelect->Combat"
                }));
        }

        private AppUiRootController CreateController()
        {
            GameObject controllerObject = CreateObject("UiRootController");
            controllerObject.SetActive(false);

            GameObject mainMenu = CreateObject("MainMenu");
            GameObject fieldSelect = CreateObject("FieldSelect");
            GameObject combat = CreateObject("Combat");

            SetPrivateField(
                controllerObject.AddComponent<AppUiRootController>(),
                "screenBindings",
                new[]
                {
                    CreateBinding(UiScreenId.MainMenu, mainMenu),
                    CreateBinding(UiScreenId.FieldSelect, fieldSelect),
                    CreateBinding(UiScreenId.Combat, combat)
                });

            controllerObject.SetActive(true);
            AppUiRootController controller =
                controllerObject.GetComponent<AppUiRootController>();
            controller.ShowMainMenu();
            return controller;
        }

        private bool IsActive(string objectName)
        {
            foreach (GameObject gameObject in _objects)
            {
                if (gameObject != null && gameObject.name == objectName)
                {
                    return gameObject.activeSelf;
                }
            }

            return false;
        }

        private GameObject CreateObject(string objectName)
        {
            GameObject gameObject = new GameObject(objectName);
            _objects.Add(gameObject);
            return gameObject;
        }

        private static UiScreenBinding CreateBinding(
            UiScreenId screenId,
            GameObject root)
        {
            UiScreenBinding binding = new UiScreenBinding();
            SetPrivateField(binding, "screenId", screenId);
            SetPrivateField(binding, "root", root);
            return binding;
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field '{fieldName}'.");
            field.SetValue(target, value);
        }
    }
}
