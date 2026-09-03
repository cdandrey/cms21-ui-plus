using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

#if NET6_0_OR_GREATER
using Il2CppCMS.UI.Logic;
#else
using CMS.UI.Logic;
#endif

namespace Cms21UiPlus
{
    internal static class RepairSkillIndicator
    {
        private const string LegacyIndicatorName = "RepairSkill";
        private const string IndicatorNamePrefix = "RepairSkillIcon";
        private const string LevelName = "RepairSkillLevel";
        private const int MaximumIndicators = 6;
        private const float IndicatorGap = 2f;
        private const float LevelGap = 0f;

        private static readonly Dictionary<string, Sprite> SkillSprites =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static bool loadFailureLogged;

        public static void Update(GameObject repairIcon, string itemId,
            Text textTemplate)
        {
            string[] indicatorPaths;
            bool ignoredAvailability;
            GameplayRepairSkillBridge.TryGetRepairDisplayData(itemId,
                out indicatorPaths, out ignoredAvailability);
            Update(repairIcon, itemId, textTemplate, indicatorPaths);
        }

        public static void Update(GameObject repairIcon, string itemId,
            Text textTemplate, string[] indicatorPaths)
        {
            if (repairIcon == null)
                return;

            Transform levelTransform = repairIcon.transform.Find(LevelName);
            int repairLevel = PartRepairabilityRules.GetRepairLevel(itemId);
            RectTransform repairRect = repairIcon.GetComponent<RectTransform>();
            float repairSize = GetRepairIconSize(repairRect);

            UpdateSkillIcons(repairIcon.transform, indicatorPaths, repairSize);

            if (repairLevel > 0)
                UpdateSkillLevel(repairIcon.transform, levelTransform,
                    repairLevel, repairSize, textTemplate);
            else if (levelTransform != null)
                levelTransform.gameObject.SetActive(false);
        }

        private static void UpdateSkillIcons(Transform parent,
            string[] paths, float repairSize)
        {
            Transform legacy = parent.Find(LegacyIndicatorName);
            if (legacy != null)
                legacy.gameObject.SetActive(false);

            int visibleCount = paths != null
                ? Mathf.Min(paths.Length, MaximumIndicators) : 0;
            for (int index = 0; index < MaximumIndicators; index++) {
                string name = IndicatorNamePrefix + (index + 1);
                Transform existing = parent.Find(name);
                if (index >= visibleCount || string.IsNullOrEmpty(paths[index])) {
                    if (existing != null)
                        existing.gameObject.SetActive(false);
                    continue;
                }

                UpdateSkillIcon(parent, existing, name, paths[index], index,
                    repairSize);
            }
        }

        private static bool UpdateSkillIcon(Transform parent,
            Transform existing, string name, string path, int visibleIndex,
            float repairSize)
        {
            Sprite sprite = GetSkillSprite(path);
            if (sprite == null) {
                if (existing != null)
                    existing.gameObject.SetActive(false);
                return false;
            }

            GameObject indicatorObject;
            Image image;
            if (existing != null) {
                indicatorObject = existing.gameObject;
                image = indicatorObject.GetComponent<Image>();
                if (image == null)
                    image = indicatorObject.AddComponent<Image>();
            } else {
                indicatorObject = new GameObject(name);
                indicatorObject.transform.SetParent(parent, false);
                image = indicatorObject.AddComponent<Image>();
            }

            image.sprite = sprite;
            image.overrideSprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            image.raycastTarget = false;

            RectTransform rect = indicatorObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f,
                -IndicatorGap - visibleIndex * (repairSize + IndicatorGap));
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(repairSize, repairSize);
            indicatorObject.SetActive(true);
            return true;
        }

        private static void UpdateSkillLevel(Transform parent,
            Transform existing, int skill, float repairSize,
            Text textTemplate)
        {
            Text text = existing != null ? existing.GetComponent<Text>() : null;
            if (text == null) {
                text = NativeUiFactory.CloneText(parent, LevelName,
                    textTemplate);
                if (text == null)
                    return;

                TextLocalize localize = text.GetComponent<TextLocalize>();
                if (localize != null)
                    GameObject.Destroy(localize);
            }

            float levelSize = repairSize * 0.5f;
            text.text = skill.ToString();
            text.fontSize = Mathf.Max(1, Mathf.RoundToInt(levelSize));
            text.fontStyle = FontStyle.Normal;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            RectTransform rect = text.GetComponent<RectTransform>();
            if (rect == null) {
                text.gameObject.SetActive(false);
                return;
            }

            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(LevelGap, 0f);
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(levelSize, levelSize);
            text.gameObject.SetActive(true);
        }


        private static float GetRepairIconSize(RectTransform rect)
        {
            if (rect == null)
                return 15f;
            float height = Mathf.Abs(rect.sizeDelta.y);
            if (height <= 0.01f)
                height = Mathf.Abs(rect.rect.height);
            return height > 0.01f ? height : 15f;
        }

        private static Sprite GetSkillSprite(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            Sprite cached;
            if (SkillSprites.TryGetValue(path, out cached)) {
                if (cached != null && cached.texture != null)
                    return cached;
                SkillSprites.Remove(path);
            }
            if (!File.Exists(path))
                return null;

            try {
                byte[] data = File.ReadAllBytes(path);
                if (data.Length == 0)
                    return null;

                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32,
                    false);
                if (!ImageConversion.LoadImage(texture, data)) {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                texture.name = Path.GetFileNameWithoutExtension(path);
                texture.wrapMode = TextureWrapMode.Clamp;
                Sprite sprite = Sprite.Create(texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                if (sprite == null) {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                sprite.name = texture.name;
                SkillSprites[path] = sprite;
                return sprite;
            } catch (Exception exception) {
                if (!loadFailureLogged) {
                    loadFailureLogged = true;
                    ModLogger.Log("[RepairSkillIndicator] Skill indicator " +
                        "loading failed." + Environment.NewLine + exception,
                        Types.LoggingLevels.Warning);
                }
                return null;
            }
        }
    }
}
