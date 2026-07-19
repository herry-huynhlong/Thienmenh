using UnityEngine;

public partial class FrontierDefenseCoordinator
{
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
                !post.signalOnBeastWaveWarning ||
                post.currentAssignee == null ||
                NpcRoleUtility.IsDead(post.currentAssignee))
            {
                continue;
            }

            SpawnSignalEffect(
                post.signalPrefab,
                post.GetSignalPosition() + post.signalSpawnOffset);
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
            origin + post.signalSpawnOffset);
    }

    void SpawnSignalEffect(
        GameObject signalPrefab,
        Vector3 position)
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
    }
}
