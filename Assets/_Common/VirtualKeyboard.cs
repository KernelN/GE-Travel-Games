using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GETravelGames.Common
{
    public sealed class VirtualKeyboard : MonoBehaviour
    {
        TMP_InputField _activeField;

        // ── Public API ──────────────────────────────────────────────────────────

        public void SetActiveField(TMP_InputField field) => _activeField = field;
        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);

        // ── Runtime wiring ──────────────────────────────────────────────────────

        void Awake() => WireButtons();

        void WireButtons()
        {
            foreach (var btn in GetComponentsInChildren<Button>(true))
            {
                var labelComp = btn.GetComponentInChildren<TextMeshProUGUI>();
                if (labelComp == null) continue;
                var label = labelComp.text;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => HandleKey(label));
            }
        }

        void HandleKey(string label)
        {
            switch (label)
            {
                case "\u2190": DoBackspace(); break; // ←
                case "ESPACIO": DoSpace(); break;
                default: TypeChar(label); break;
            }
        }

        // ── Key actions ─────────────────────────────────────────────────────────

        void TypeChar(string c)
        {
            if (_activeField == null) return;
            if (_activeField.contentType == TMP_InputField.ContentType.IntegerNumber)
                if (c.Length != 1 || !char.IsDigit(c[0])) return;
            _activeField.text += c;
            _activeField.caretPosition = _activeField.text.Length;
        }

        void DoBackspace()
        {
            if (_activeField == null || _activeField.text.Length == 0) return;
            _activeField.text = _activeField.text[..^1];
            _activeField.caretPosition = _activeField.text.Length;
        }

        void DoSpace()
        {
            if (_activeField == null) return;
            if (_activeField.contentType == TMP_InputField.ContentType.IntegerNumber) return;
            _activeField.text += " ";
            _activeField.caretPosition = _activeField.text.Length;
        }

        // ── Editor UI builder ───────────────────────────────────────────────────

#if UNITY_EDITOR
        static readonly string[] SymbolRow  = { "/", "(", ")", "#", ":", "*", "$", "%" };
        static readonly string[] QwertyRow1 = { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P" };
        static readonly string[] QwertyRow2 = { "A", "S", "D", "F", "G", "H", "J", "K", "L", "\u00d1" };
        static readonly string[] QwertyRow3 = { "Z", "X", "C", "V", "B", "N", "M", ".", "-", "\u2013" };
        static readonly string[] NumRow1    = { "7", "8", "9" };
        static readonly string[] NumRow2    = { "4", "5", "6" };
        static readonly string[] NumRow3    = { "1", "2", "3" };
        static readonly string[] NumRow4    = { "0", "+" };

        public void Build()
        {
            // ── Panel setup (this GameObject) ────────────────────────────────────
            var img = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            img.color = UIBuilderHelper.ColPanel;

            var rt = GetComponent<RectTransform>();
            UIBuilderHelper.SetAnchored(rt,
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(0, 0), new Vector2(0, 220));

            // ── Outer HLG ────────────────────────────────────────────────────────
            var outerHlg = gameObject.GetComponent<HorizontalLayoutGroup>()
                           ?? gameObject.AddComponent<HorizontalLayoutGroup>();
            outerHlg.childAlignment      = TextAnchor.MiddleCenter;
            outerHlg.spacing             = 8;
            outerHlg.padding             = new RectOffset(6, 6, 6, 6);
            outerHlg.childControlWidth   = true;
            outerHlg.childControlHeight  = true;
            outerHlg.childForceExpandWidth  = true;
            outerHlg.childForceExpandHeight = true;

            // ── Letters section ──────────────────────────────────────────────────
            var letters = MakeSection(transform, "LettersSection", 2.5f, TextAnchor.UpperCenter);
            BuildRow(letters.transform, "SymbolRow",  SymbolRow,  Row4Config.None);
            BuildRow(letters.transform, "QwertyRow1", QwertyRow1, Row4Config.None);
            BuildRow(letters.transform, "QwertyRow2", QwertyRow2, Row4Config.None);
            BuildRow(letters.transform, "QwertyRow3", QwertyRow3, Row4Config.None);
            BuildSpaceRow(letters.transform);

            // ── Divider ──────────────────────────────────────────────────────────
            var divGo = new GameObject("Divider", typeof(RectTransform));
            divGo.transform.SetParent(transform, false);
            divGo.AddComponent<Image>().color = UIBuilderHelper.ColBg;
            var divLe = divGo.AddComponent<LayoutElement>();
            divLe.preferredWidth = 8;
            divLe.flexibleWidth  = 0;

            // ── Numpad section ───────────────────────────────────────────────────
            var numpad = MakeSection(transform, "NumpadSection", 1f, TextAnchor.LowerCenter);
            BuildRow(numpad.transform, "NumRow1", NumRow1, Row4Config.None);
            BuildRow(numpad.transform, "NumRow2", NumRow2, Row4Config.None);
            BuildRow(numpad.transform, "NumRow3", NumRow3, Row4Config.None);
            BuildRow(numpad.transform, "NumRow4", NumRow4, Row4Config.None);
        }

        // ── Private builder helpers ─────────────────────────────────────────────

        enum Row4Config { None }

        static GameObject MakeSection(Transform parent, string name,
            float flexWidth, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = Color.clear;

            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = flexWidth;

            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment       = alignment;
            vlg.spacing              = 4;
            vlg.padding              = new RectOffset(0, 0, 0, 0);
            vlg.childControlWidth    = true;
            vlg.childControlHeight   = false;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            return go;
        }

        static GameObject MakeKeyRow(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = Color.clear;

            UIBuilderHelper.AddLayout(go, preferredHeight: 38);

            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment       = TextAnchor.MiddleCenter;
            hlg.spacing              = 4;
            hlg.padding              = new RectOffset(0, 0, 0, 0);
            hlg.childControlWidth    = true;
            hlg.childControlHeight   = true;
            hlg.childForceExpandWidth  = true;
            hlg.childForceExpandHeight = true;
            return go;
        }

        static Button MakeKey(Transform rowParent, string label,
            float flexWidth = 1f, Color? bgOverride = null)
        {
            var btn = UIBuilderHelper.MakeButton(rowParent, "Key_" + label, label,
                bgOverride ?? UIBuilderHelper.ColBtnSmall,
                UIBuilderHelper.ColTextPrimary, 18, FontStyles.Bold);

            var le = btn.gameObject.AddComponent<LayoutElement>();
            le.flexibleWidth  = flexWidth;
            le.flexibleHeight = 1;
            return btn;
        }

        static void BuildRow(Transform parent, string rowName, string[] keys, Row4Config _)
        {
            var row = MakeKeyRow(parent, rowName);
            foreach (var k in keys)
                MakeKey(row.transform, k);
        }

        static void BuildSpaceRow(Transform parent)
        {
            var row = MakeKeyRow(parent, "SpaceRow");
            MakeKey(row.transform, "@");
            MakeKey(row.transform, "ESPACIO", flexWidth: 5f);
            MakeKey(row.transform, "\u2190", flexWidth: 1.5f,  // ←
                bgOverride: UIBuilderHelper.ColBtnSecondary);
        }
#endif
    }
}
