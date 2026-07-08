using UnityEngine;

public static class CameraWorldPlaneUtility
{
    public static Vector3 ScreenToWorldOnPlane(
        Camera camera,
        Vector2 screenPosition,
        float worldZ = 0f)
    {
        if (camera == null)
        {
            return new Vector3(screenPosition.x, screenPosition.y, worldZ);
        }

        if (camera.orthographic)
        {
            Vector3 worldPosition =
                camera.ScreenToWorldPoint(
                    new Vector3(
                        screenPosition.x,
                        screenPosition.y,
                        Mathf.Abs(worldZ - camera.transform.position.z)));

            worldPosition.z = worldZ;
            return worldPosition;
        }

        Ray ray =
            camera.ScreenPointToRay(
                new Vector3(
                    screenPosition.x,
                    screenPosition.y,
                    0f));

        Plane plane =
            new Plane(
                Vector3.forward,
                new Vector3(0f, 0f, worldZ));

        if (plane.Raycast(ray, out float enter))
        {
            Vector3 worldPosition = ray.GetPoint(enter);
            worldPosition.z = worldZ;
            return worldPosition;
        }

        Vector3 fallback =
            camera.ScreenToWorldPoint(
                new Vector3(
                    screenPosition.x,
                    screenPosition.y,
                    Mathf.Abs(worldZ - camera.transform.position.z)));

        fallback.z = worldZ;
        return fallback;
    }
}
