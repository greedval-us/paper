using System.Linq;
using NUnit.Framework;
using PaperDefense.Application;
using PaperDefense.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace PaperDefense.Tests
{
    public sealed class CatalogHudTests
    {
        [Test] public void TargetWorkshopExplainsArmorAndFiniteIncome()
        {
            var root = new GameObject("Isolated target HUD"); root.SetActive(false);
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.useTargetWorkshopPreset = true;
                var run = root.AddComponent<RunController>(); run.balanceAsset = asset; run.InitializeWorkshop(false);
                root.AddComponent<WorkshopHud>().Initialize(run);
                root.GetComponentsInChildren<Button>(true).Single(b => b.name == "Module Armor").onClick.Invoke();
                var texts = root.GetComponentsInChildren<Text>(true);
                Assert.That(texts.Any(t => t.text.Contains("защита 25%")), Is.True);
                Assert.That(texts.Any(t => t.text.Contains("ожидание не даёт штрихов")), Is.True);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(asset); }
        }

        [Test] public void AssemblyButtonsDescribeConfiguredCapacityAndModuleLoad()
        {
            var root = new GameObject("Isolated catalog HUD"); root.SetActive(false);
            var asset = ScriptableObject.CreateInstance<GameBalanceAsset>();
            try
            {
                asset.construction.StandardCapacity = 17; asset.construction.HeavyCapacity = 23;
                asset.construction.ArmorLoad = 5;
                var run = root.AddComponent<RunController>(); run.balanceAsset = asset; run.InitializeWorkshop(false);
                root.AddComponent<WorkshopHud>().Initialize(run);
                var buttons = root.GetComponentsInChildren<Button>(true);
                Assert.That(buttons.Single(b => b.name == "Chassis Standard").GetComponentInChildren<Text>(true).text, Does.Contain("17"));
                Assert.That(buttons.Single(b => b.name == "Chassis Heavy").GetComponentInChildren<Text>(true).text, Does.Contain("23"));
                Assert.That(buttons.Single(b => b.name == "Module Armor").GetComponentInChildren<Text>(true).text, Does.Contain("5"));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(asset); }
        }
    }
}
