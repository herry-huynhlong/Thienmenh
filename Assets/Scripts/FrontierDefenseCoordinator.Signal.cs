using UnityEngine;

public partial class FrontierDefenseCoordinator
{
    const int SignalFallbackSortingOrder = 80;
    const int SignalSortingBoost = 40;

    void TriggerWaveWarningSignals(ActiveBeastWave wave)
    {
        if (wave == null)
        {
            return;
        }

        for (int i = 0; i < posts.Count; i++)
        {
            FrontierWatchPost post = posts[i];
            if (post == null ||
                !post.isActiveAndEnabled ||
                post.signalPrefab == null ||
                !post.signalOnBeastWaveWarning)
            {
                continue;
            }

            GameObject signalSource =
                post.currentAssignee != null &&
                !NpcRoleUtility.IsDead(post.currentAssignee)
                    ? post.currentAssignee
                    : null;
            Vector3 origin =
                signalSource != null
                    ? signalSource.transform.position
                    : post.GetSignalPosition();
            SpawnSignalEffect(
                post.signalPrefab,
                origin + post.signalSpawnOffset,
                signalSource != null
                    ? signalSource.transform
                    : post.transform);
        }
    }

    void TriggerWatcherDeathSignal(
        FrontierWatchPost post,
        GameObject npc)
    {
        if (post == null ||
            post.signalPrefab == null ||
            !post.signalOnWatcherDeath)
        {
            return;
        }

        Vector3 origin =
            npc != null
                ? npc.transform.position
                : post.GetSignalPosition();
        SpawnSignalEffect(
            post.signalPrefab,
            origin + post.signalSpawnOffset,
            npc != null
                ? npc.transform
                : post.transform);
    }

    void SpawnSignalEffect(
        GameObject signalPrefab,
        Vector3 position,
        Transform visualAnchor)
    {
        if (signalPrefab == null)
        {
            return;
        }

        GameObject spawned =
            Instantiate(
                signalPrefab,
                position,
                Quaternion.identity);
        WorldSignalEffect effect =
            spawned.GetComponent<WorldSignalEffect>();
        if (effect != null)
        {
            effect.PlayNow();
        }

        ApplySignalSorting(
            spawned.GetComponent<SpriteRenderer>(),
            visualAnchor);
    }

    void ApplySignalSorting(
        SpriteRenderer signalRenderer,
        Transform visualAnchor)
    {
        if (signalRenderer == null)
        {
            return;
        }

        int bestSortingLayerId = signalRenderer.sortingLayerID;
        int bestSortingOrder =
            Mathf.Max(
                SignalFallbackSortingOrder,
                signalRenderer.sortingOrder);
        bool foundAnchorRenderer = false;

        if (visualAnchor != null)
        {
            SpriteRenderer[] anchorRenderers =
                visualAnchor.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < anchorRenderers.Length; i++)
            {
                SpriteRenderer anchorRenderer = anchorRenderers[i];
                if (anchorRenderer == null)
                {
                    continue;
                }

                foundAnchorRenderer = true;
                if (anchorRenderer.sortingOrder > bestSortingOrder)
                {
                    bestSortingLayerId = anchorRenderer.sortingLayerID;
                    bestSortingOrder = anchorRenderer.sortingOrder;
                }
            }
        }

        signalRenderer.sortingLayerID =
            foundAnchorRenderer
                ? bestSortingLayerId
                : signalRenderer.sortingLayerID;
        signalRenderer.sortingOrder =
            Mathf.Max(
                SignalFallbackSortingOrder,
                bestSortingOrder + SignalSortingBoost);
    }
}
