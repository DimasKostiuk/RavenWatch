#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace HuginWhispers
{
    // The story window (StoryPanel), «Актори»: our NPCs, made and ordered by hand (a scene-test tool).
    internal partial class StoryPanel
    {
        // ---------------------------------------------------------------- «Актори»: our NPCs, made and ordered by hand
        private void Actors()
        {
            Player p = Player.m_localPlayer;
            string me = p.GetZDOID().UserID + ":" + p.GetZDOID().ID;
            if (_npc == null || !NpcActor.Live.Contains(_npc)) _npc = null;
            GUILayout.BeginHorizontal();

            // left: who can be made (templates) and the place to make them at
            GUILayout.BeginVertical(GUILayout.Width(250f));
            GUILayout.Label("1 · Кого створити (шаблони)", _head);
            _defScroll = GUILayout.BeginScrollView(_defScroll, GUILayout.Height(230f));
            foreach (NpcDef d in NpcDefs.All())
                if (GUILayout.Button(d.Name + "  (" + d.Id + (d.Odin ? ", Одін" : "") + ")", d.Id == _npcDef ? _sel : GUI.skin.button)) _npcDef = d.Id;
            GUILayout.EndScrollView();
            if (GUILayout.Button("Перечитати шаблони з файлів")) { NpcDefs.Reload(); Note = "Шаблонів: " + NpcDefs.All().Count + " (файли: config/RavenWatch/npc/*.txt)."; }
            GUILayout.BeginHorizontal();
            GUILayout.Label("Тег у сцені:", GUILayout.Width(90f));
            _npcTag = GUILayout.TextField(_npcTag, 32);
            GUILayout.EndHorizontal();
            GUILayout.Label("Тег — ім'я актора для сцени (vidlunnia, odin…). Можна порожнім.", _hint);
            GUI.enabled = _npcDef.Length > 0;
            if (GUILayout.Button("Створити переді мною", GUILayout.Height(28f)))
            {
                NpcActor a = Npcs.Spawn(_npcDef, p.transform.position + p.transform.forward * 3f, Quaternion.LookRotation(-p.transform.forward), p.GetPlayerID(), _npcTag);
                Note = a != null ? "Створено: " + a.Describe(p) : "Не вийшло (див. лог: Npc:).";
                if (a != null) _npc = a;
            }
            StoryPlaces.Place pl = _npcPlace.Length > 0 ? StoryPlaces.Find(_npcPlace) : null;
            GUI.enabled = _npcDef.Length > 0 && pl != null;
            if (GUILayout.Button(pl != null ? "Створити на мітці «" + pl.Name + "»" : "Створити на мітці (обери мітку нижче)", GUILayout.Height(28f)))
            {
                NpcActor a = Npcs.Spawn(_npcDef, pl.Pos, Quaternion.Euler(0f, pl.Yaw, 0f), p.GetPlayerID(), _npcTag);
                Note = a != null ? "Створено на «" + pl.Name + "»: " + a.Describe(p) : "Не вийшло (див. лог: Npc:).";
                if (a != null) _npc = a;
            }
            GUI.enabled = true;
            GUILayout.Space(6);
            GUILayout.Label("Мітка (для «створити на мітці» і «іди до мітки»):", _hint);
            _npcPlaceScroll = GUILayout.BeginScrollView(_npcPlaceScroll, GUILayout.Height(160f));
            if (StoryPlaces.All.Count == 0) GUILayout.Label("Міток нема — постав у «Кресленні», крок 3.", _hint);
            foreach (StoryPlaces.Place x in StoryPlaces.All)
                if (GUILayout.Button(x.Name + "  · " + Mathf.RoundToInt(Vector3.Distance(p.transform.position, x.Pos)) + " м", x.Name == _npcPlace ? _sel : GUI.skin.button)) _npcPlace = x.Name;
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.Space(12);
            // middle: who is alive near you
            GUILayout.BeginVertical(GUILayout.Width(250f));
            GUILayout.Label("2 · Живі поруч (" + NpcActor.Live.Count + ")", _head);
            _liveScroll = GUILayout.BeginScrollView(_liveScroll, GUILayout.Height(420f));
            if (NpcActor.Live.Count == 0) GUILayout.Label("Нікого. Створи зліва.", _hint);
            foreach (NpcActor n in NpcActor.Live)
                if (n != null && GUILayout.Button(n.Describe(p), n == _npc ? _sel : GUI.skin.button)) _npc = n;
            GUILayout.EndScrollView();
            if (GUILayout.Button("Обрати найближчого")) { _npc = Npcs.Nearest(p.transform.position, 60f); Note = _npc != null ? "Обрано: " + _npc.Describe(p) : "Нікого в 60 м."; }
            GUI.enabled = NpcActor.Live.Count > 0;
            if (GUILayout.Button("Прибрати всіх")) { int gone = 0; foreach (NpcActor x in NpcActor.Live.ToArray()) if (x != null) { x.Remove(); gone++; } _npc = null; Note = "Прибрано: " + gone + "."; }
            GUI.enabled = true;
            GUILayout.EndVertical();

            GUILayout.Space(12);
            // right: orders to the picked one — in a scroll of its own, a few buttons a row (it was cut off on the right)
            GUILayout.BeginVertical();
            _ordersScroll = GUILayout.BeginScrollView(_ordersScroll);
            GUILayout.Label("3 · Накази" + (_npc != null ? ": " + _npc.Id + (_npc.Tag.Length > 0 ? " «" + _npc.Tag + "»" : "") : ""), _head);
            if (_npc == null) GUILayout.Label("Обери актора посередині.", _hint);
            else
            {
                Vector3 aim = Npcs.Aim(p);
                GUILayout.Label("Зараз: " + (_npc.Mind != null ? _npc.Mind.Describe() : "без розуму (Одін стоїть)"), _hint);
                GUILayout.Label("Рух", _head);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("До мене")) _npc.Order("go " + Npcs.V(p.transform.position) + " run");
                if (GUILayout.Button("Туди, куди дивлюсь")) _npc.Order("go " + Npcs.V(aim));
                if (GUILayout.Button("За мною")) _npc.Order("follow " + me + " 3");
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Стій")) _npc.Order("stay");
                if (GUILayout.Button("Відпусти")) _npc.Order("free"); // (the order let go: back to its own day)
                if (GUILayout.Button("Блукай тут (8 м)")) _npc.Order("wander 8 " + Npcs.V(_npc.transform.position));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUI.enabled = pl != null;
                if (GUILayout.Button(pl != null ? "Іди до мітки «" + pl.Name + "»" : "Іди до мітки (обери зліва)")) _npc.Order("go " + Npcs.V(pl.Pos));
                if (GUILayout.Button("Сядь біля мітки")) _npc.Order("sit " + Npcs.V(pl.Pos));
                GUI.enabled = true;
                if (GUILayout.Button("Переставити до мене")) _npc.Order("tp " + Npcs.V(p.transform.position + p.transform.forward * 2f));
                GUILayout.EndHorizontal();
                GUILayout.Label("Погляд і поза", _head);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Дивись на мене")) _npc.Order("look " + me);
                if (GUILayout.Button("Не дивись")) _npc.Order("look none");
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Сядь (де дивлюсь)")) _npc.Order("sit " + Npcs.V(aim));
                if (GUILayout.Button("Встань")) _npc.Order("stand");
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                for (int i = 0; i < Emotes.Length; i++)
                {
                    if (i > 0 && i % 4 == 0) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
                    if (GUILayout.Button(Emotes[i])) _npc.Order("emote " + Emotes[i]);
                }
                GUILayout.EndHorizontal();
                GUILayout.Label("Мова", _head);
                GUILayout.BeginHorizontal();
                _npcSay = GUILayout.TextField(_npcSay, 200);
                if (GUILayout.Button("Скажи", GUILayout.Width(80f))) _npc.Say(_npcSay);
                GUILayout.EndHorizontal();
                GUILayout.Label("Бій", _head);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Тікати")) _npc.Order("fight flee"); // (peaceful: the default)
                if (GUILayout.Button("Обережний")) _npc.Order("fight wary"); // (runs, fights back when caught)
                if (GUILayout.Button("Не битись")) _npc.Order("fight off");
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Захищатись")) _npc.Order("fight defend");
                if (GUILayout.Button("Полювати")) _npc.Order("fight hunt");
                GUILayout.EndHorizontal();
                GUILayout.Label("Мешканець", _head);
                NpcResident res = _npc.GetComponent<NpcResident>();
                if (res != null)
                {
                    GUILayout.Label(res.Describe(), _hint);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Оселити тут (де стоїть)")) Note = NpcResident.Settle(_npc, _npc.transform.position, false);
                    if (GUILayout.Button("Оселити біля мене")) Note = NpcResident.Settle(_npc, p.transform.position, false);
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Виселити")) Note = NpcResident.Unsettle(_npc);
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Його ліжко — куди дивлюсь")) Note = NpcResident.GiveBed(_npc, aim);
                    GUI.enabled = NpcResident.Is(_npc);
                    if (GUILayout.Button("Примха зараз")) Note = NpcWhims.Force(res, NpcWhims.All[UnityEngine.Random.Range(0, NpcWhims.All.Count)].Id);
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
                GUILayout.Space(8);
                if (GUILayout.Button("Прибрати цього")) { _npc.Remove(); _npc = null; }
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private Vector2 _ordersScroll;
    }
}
#endif
