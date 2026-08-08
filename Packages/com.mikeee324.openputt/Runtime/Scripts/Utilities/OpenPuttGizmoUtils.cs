using UnityEngine;

namespace dev.mikeee324.OpenPutt
{
    public static class OpenPuttGizmoUtils
    {
#if COMPILER_UDONSHARP
        public static void DrawWireCollider(Collider collider)
        {
        }

        public static void DrawWireCollider(GameObject gameObject)
        {
        }

        public static void DrawWireBoxCollider(BoxCollider boxCollider)
        {
        }

        public static void DrawWireSphereCollider(SphereCollider sphereCollider)
        {
        }

        public static void DrawWireCapsuleCollider(CapsuleCollider capsuleCollider)
        {
        }

        public static void DrawWireMeshCollider(MeshCollider meshCollider)
        {
        }

        public static void DrawSolidAndWireBoxCollider(BoxCollider boxCollider, Color solidColor, Color wireColor)
        {
        }

        public static void DrawSphereMarker(Vector3 position, float radius, Color color)
        {
        }

        public static void DrawCubeArrow(Vector3 directionStart, Vector3 directionEnd, Color color, float thickness)
        {
        }
#else
        public static void DrawWireCollider(Collider collider)
        {
            if (collider == null) return;

            var boxCollider = collider as BoxCollider;
            if (boxCollider != null)
            {
                DrawWireBoxCollider(boxCollider);
                return;
            }

            var sphereCollider = collider as SphereCollider;
            if (sphereCollider != null)
            {
                DrawWireSphereCollider(sphereCollider);
                return;
            }

            var capsuleCollider = collider as CapsuleCollider;
            if (capsuleCollider != null)
            {
                DrawWireCapsuleCollider(capsuleCollider);
                return;
            }

            var meshCollider = collider as MeshCollider;
            if (meshCollider != null)
            {
                DrawWireMeshCollider(meshCollider);
                return;
            }
        }

        public static void DrawWireCollider(GameObject gameObject)
        {
            if (gameObject == null) return;
            DrawWireCollider(gameObject.GetComponent<Collider>());
        }

        public static void DrawWireBoxCollider(BoxCollider boxCollider)
        {
            if (boxCollider == null) return;

            var previousMatrix = Gizmos.matrix;
            Gizmos.matrix = boxCollider.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
            Gizmos.matrix = previousMatrix;
        }

        public static void DrawWireSphereCollider(SphereCollider sphereCollider)
        {
            if (sphereCollider == null) return;

            var previousMatrix = Gizmos.matrix;
            Gizmos.matrix = sphereCollider.transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(sphereCollider.center, sphereCollider.radius);
            Gizmos.matrix = previousMatrix;
        }

        public static void DrawWireCapsuleCollider(CapsuleCollider capsuleCollider)
        {
            if (capsuleCollider == null) return;

            var previousMatrix = Gizmos.matrix;
            Gizmos.matrix = capsuleCollider.transform.localToWorldMatrix;

            var center = capsuleCollider.center;
            var radius = capsuleCollider.radius;
            var halfCylinder = Mathf.Max(0f, capsuleCollider.height * 0.5f - radius);

            Vector3 axis;
            Vector3 perpendicularA;
            Vector3 perpendicularB;

            switch (capsuleCollider.direction)
            {
                case 0:
                    axis = Vector3.right;
                    perpendicularA = Vector3.up;
                    perpendicularB = Vector3.forward;
                    break;
                case 2:
                    axis = Vector3.forward;
                    perpendicularA = Vector3.right;
                    perpendicularB = Vector3.up;
                    break;
                default:
                    axis = Vector3.up;
                    perpendicularA = Vector3.right;
                    perpendicularB = Vector3.forward;
                    break;
            }

            var topCenter = center + axis * halfCylinder;
            var bottomCenter = center - axis * halfCylinder;

            Gizmos.DrawWireSphere(topCenter, radius);
            Gizmos.DrawWireSphere(bottomCenter, radius);

            Gizmos.DrawLine(topCenter + perpendicularA * radius, bottomCenter + perpendicularA * radius);
            Gizmos.DrawLine(topCenter - perpendicularA * radius, bottomCenter - perpendicularA * radius);
            Gizmos.DrawLine(topCenter + perpendicularB * radius, bottomCenter + perpendicularB * radius);
            Gizmos.DrawLine(topCenter - perpendicularB * radius, bottomCenter - perpendicularB * radius);

            Gizmos.matrix = previousMatrix;
        }

        public static void DrawWireMeshCollider(MeshCollider meshCollider)
        {
            if (meshCollider == null || meshCollider.sharedMesh == null) return;

            var colliderTransform = meshCollider.transform;
            Gizmos.DrawWireMesh(meshCollider.sharedMesh, -1, colliderTransform.position, colliderTransform.rotation, colliderTransform.lossyScale);
        }

        public static void DrawSolidAndWireBoxCollider(BoxCollider boxCollider, Color solidColor, Color wireColor)
        {
            if (boxCollider == null) return;

            var previousMatrix = Gizmos.matrix;
            Gizmos.matrix = boxCollider.transform.localToWorldMatrix;

            Gizmos.color = solidColor;
            Gizmos.DrawCube(boxCollider.center, boxCollider.size);

            Gizmos.color = wireColor;
            Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);

            Gizmos.matrix = previousMatrix;
        }

        public static void DrawSphereMarker(Vector3 position, float radius, Color color)
        {
            Gizmos.color = color;
            Gizmos.DrawSphere(position, radius);
            Gizmos.DrawWireSphere(position, radius);
        }

        public static void DrawCubeArrow(Vector3 directionStart, Vector3 directionEnd, Color color, float thickness)
        {
            DrawTransparentCubeLine(directionStart, directionEnd, color, thickness);

            var arrowDir = (directionEnd - directionStart).normalized;
            var arrowHeadLength = 0.2f;
            var arrowHeadAngle = 30f;

            var left = Quaternion.LookRotation(arrowDir) * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) * Vector3.forward;
            var right = Quaternion.LookRotation(arrowDir) * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) * Vector3.forward;

            DrawTransparentCubeLine(directionEnd, directionEnd + left * arrowHeadLength, color, thickness);
            DrawTransparentCubeLine(directionEnd, directionEnd + right * arrowHeadLength, color, thickness);
        }

        private static void DrawTransparentCubeLine(Vector3 start, Vector3 end, Color color, float thickness)
        {
            var direction = end - start;
            var distance = direction.magnitude;
            if (distance <= 0.0001f) return;

            var center = (start + end) / 2f;
            var rotation = Quaternion.LookRotation(direction);
            var scale = new Vector3(thickness, thickness, distance);

            Gizmos.color = color;

            var previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(center, rotation, scale);
            Gizmos.DrawCube(Vector3.zero, Vector3.one);
            Gizmos.matrix = previousMatrix;
        }
#endif
    }
}