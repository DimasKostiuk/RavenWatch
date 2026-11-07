#if RAVEN_ITEM && RAVEN_STORY
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HuginWhispers
{
    // The stage's fixtures (the user 2026-11-04: «such checks would help to make other animations later»): what a clip's body touches,
    // set before the mannequin as it stands in its scene, so the check measures the touch (BodyCheck «touch») and the pictures show it:
    //   raven stage wall <m> [width height]          a wall, its near face that far ahead (a door, a gate)
    //   raven stage table <height> <m> [depth width]  a table, its top that high, its near edge that far ahead (a letter, a book)
    //   raven stage seat <height> [z]                 a seat under it (z: its middle, behind the feet by default) — the hips onto it
    //   raven stage step <height> <m>                 a step that high, its near edge that far ahead — a foot onto it
    //   raven stage point <x> <y> <z>                 a point in its frame (a lock, a handle, a hook) — how near a hand comes
    //   raven stage piece <prefab> <m> [turn]         the game's own piece (its look only: no net, no touch of its own), its near
    //                                                 face that far ahead — what a scene really has there (stave_gate: the Hall's)
    //   raven stage clear                             all of them away
    // A clip's own «touch» line is the same words: «play» sets it up by itself and takes it away for a clip with another.
    // Each is a box in the body's frame (a piece: its look's bounds). Drawn see-through (a piece as it is), kept where the mannequin
    // stands as it is moved or turned. Only the author's stage: nothing of it in the world, nothing sent.
    internal sealed partial class AnimStage
    {
        private sealed class Fix { public BodyCheck.Fixture F; public GameObject Look; public Vector3 LookAt; public float LookTurn; }
        private readonly List<Fix> _fixtures = new List<Fix>();

        private string FixtureCmd(string what, string[] a)
        {
            if (what == "clear") { ClearFixtures(); return "Пристосування прибрано."; }
            Fix x = new Fix { F = new BodyCheck.Fixture { Kind = what } };

            switch (what)
            {
                case "wall": { float m = Arg(a, 2, 0.55f), w = Arg(a, 3, 1.4f), h = Arg(a, 4, 2.4f); x.F.Center = new Vector3(0f, h * 0.5f, m + 0.025f); x.F.Size = new Vector3(w, h, 0.05f); break; }
                case "table": { float h = Arg(a, 2, 0.8f), m = Arg(a, 3, 0.35f), d = Arg(a, 4, 0.6f), w = Arg(a, 5, 1.2f); x.F.Center = new Vector3(0f, h * 0.5f, m + d * 0.5f); x.F.Size = new Vector3(w, h, d); break; }
                case "seat": { float h = Arg(a, 2, 0.45f), z = Arg(a, 3, -0.15f); x.F.Center = new Vector3(0f, h * 0.5f, z); x.F.Size = new Vector3(0.5f, h, 0.45f); break; }
                case "step": { float h = Arg(a, 2, 0.25f), m = Arg(a, 3, 0.3f); x.F.Center = new Vector3(0f, h * 0.5f, m + 0.175f); x.F.Size = new Vector3(1f, h, 0.35f); break; }
                case "point": x.F.Center = new Vector3(Arg(a, 2, 0f), Arg(a, 3, 1.1f), Arg(a, 4, 0.45f)); x.F.Size = Vector3.one * 0.06f; break;
                case "piece":
                    {
                        if (a.Length < 3) return "raven stage piece <префаб> <м> [поворот]";
                        string why = Piece(x, a[2], Arg(a, 3, 0.55f), Arg(a, 4, 0f));
                        if (why != null) return why;
                        break;
                    }
                default: return null;
            }
            int same = 0; foreach (Fix f in _fixtures) if (f.F.Kind == what) same++;
            x.F.Name = same == 0 ? what : what + (same + 1);
            if (x.Look == null) x.Look = Box(x.F, what == "point");
            _fixtures.Add(x);
            PlaceFixtures();
            return x.F.Name + ": коробка " + V(x.F.Size) + " м, середина " + V(x.F.Center) + " (у системі манекена). check міряє дотик до неї.";
        }

        private static float Arg(string[] a, int i, float def) { return a.Length > i ? Num(a[i], def) : def; }

        // a see-through box (or a ball for a point) where the fixture is
        private static GameObject Box(BodyCheck.Fixture f, bool ball)
        {
            GameObject g = GameObject.CreatePrimitive(ball ? PrimitiveType.Sphere : PrimitiveType.Cube);
            g.name = "RavenWatch_Stage_" + f.Kind;
            Collider c = g.GetComponent<Collider>();
            if (c != null) Destroy(c); // (only seen and measured against: nothing bumps into it)
            Renderer r = g.GetComponent<Renderer>();
            Shader see = Shader.Find("Sprites/Default"); // (the game's one see-through shader that is always there: NpcFade; the primitive's own shows magenta)
            if (r != null && see != null) r.material = new Material(see) { color = ball ? new Color(1f, 0.85f, 0.3f, 0.7f) : new Color(0.75f, 0.75f, 0.78f, 0.35f) };
            return g;
        }

        // the game's piece, its look only (meshes copied: no net object, no colliders, no scripts), its near face m ahead
        private string Piece(Fix x, string prefab, float m, float turn)
        {
            GameObject src = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefab) : null;
            if (src == null) return "Нема префабу «" + prefab + "».";
            GameObject look = new GameObject("RavenWatch_Stage_piece_" + prefab);
            int n = 0;
            foreach (MeshFilter mf in src.GetComponentsInChildren<MeshFilter>(true)) // heavy: once, as the author asks for it
            {
                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || mf.sharedMesh == null || mf.gameObject.layer == LayerMask.NameToLayer("piece_nonsolid") && mf.name.Contains("collider")) continue;
                GameObject part = new GameObject(mf.name);
                part.transform.SetParent(look.transform, false);
                part.transform.localPosition = src.transform.InverseTransformPoint(mf.transform.position);
                part.transform.localRotation = Quaternion.Inverse(src.transform.rotation) * mf.transform.rotation;
                part.transform.localScale = mf.transform.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = mr.sharedMaterials;
                n++;
            }
            if (n == 0) { Destroy(look); return "У «" + prefab + "» нема що показати."; }
            x.Look = look; x.LookTurn = turn;
            // its box in the body's frame: its look's bounds as it stands turned, its near face put m ahead
            look.transform.position = Vector3.zero; look.transform.rotation = Quaternion.Euler(0f, turn, 0f);
            Bounds b = new Bounds(); bool any = false;
            foreach (Renderer r in look.GetComponentsInChildren<Renderer>()) { if (any) b.Encapsulate(r.bounds); else { b = r.bounds; any = true; } } // heavy: once, as the author asks for the piece
            x.LookAt = new Vector3(0f, -b.min.y, m - b.min.z); // (its foot on the ground, its near face at m)
            x.F.Center = b.center + x.LookAt; x.F.Size = b.size;
            return null;
        }

        // each where the mannequin stands now (a fixture is in its frame: moved and turned with it)
        private void PlaceFixtures()
        {
            if (_dummy == null) return;
            Transform r = _dummy.transform;
            foreach (Fix x in _fixtures)
            {
                if (x.Look == null) continue;
                if (x.F.Kind == "piece") { x.Look.transform.rotation = r.rotation * Quaternion.Euler(0f, x.LookTurn, 0f); x.Look.transform.position = r.TransformPoint(x.LookAt); }
                else { x.Look.transform.rotation = r.rotation; x.Look.transform.localScale = x.F.Size; x.Look.transform.position = r.TransformPoint(x.F.Center); }
            }
        }

        private void ClearFixtures() { foreach (Fix x in _fixtures) if (x.Look != null) Destroy(x.Look); _fixtures.Clear(); _touchFix = null; _touchWords = ""; }

        // what the clip about to play says it touches
        private Fix _touchFix;
        private string _touchWords = "";
        private void TouchFixture(PAnimClip c)
        {
            string words = c != null ? c.Touch : "";
            if (words == _touchWords) return;
            if (_touchFix != null) { if (_touchFix.Look != null) Destroy(_touchFix.Look); _fixtures.Remove(_touchFix); _touchFix = null; }
            _touchWords = words;
            if (words.Length == 0) return;
            string[] w = ("stage " + words).Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            int had = _fixtures.Count;
            Say(FixtureCmd(w[1], w)); // (a piece that is not there: said)
            if (_fixtures.Count > had) _touchFix = _fixtures[_fixtures.Count - 1];
        }

        // into a check: each fixture a box the body's touch is measured to
        private void FixturesInto(BodyCheck c) { foreach (Fix x in _fixtures) c.Fixtures.Add(x.F); }
    }
}
#endif
