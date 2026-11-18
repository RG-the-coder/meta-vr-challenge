using UnityEngine;
using UnityEngine.UI;

namespace BattlefieldPrototype
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleUiPointerGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Disc(mesh, rectTransform.rect.width * .5f, Color.black);
            Disc(mesh, rectTransform.rect.width * .33f, color);
        }
        static void Disc(VertexHelper mesh, float radius, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(Vector3.zero, tint, Vector2.zero);
            for (int i = 0; i <= 24; i++)
            {
                float angle = i * Mathf.PI * 2 / 24;
                mesh.AddVert(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
                if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}
