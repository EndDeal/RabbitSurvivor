namespace RabbitSurvivor
{
    internal readonly struct StageDefinition
    {
        internal readonly int Id;
        internal readonly string Name;
        internal readonly int DisplayOrder;
        internal readonly int PreviewIndex;
        internal readonly bool IsPlayable;
        internal readonly string SceneName;

        internal StageDefinition(int id, string name, int displayOrder, int previewIndex, bool isPlayable, string sceneName)
        {
            Id = id;
            Name = name;
            DisplayOrder = displayOrder;
            PreviewIndex = previewIndex;
            IsPlayable = isPlayable;
            SceneName = sceneName;
        }
    }

    internal static class StageCatalog
    {
        internal const int ShallowSeaId = 1001;

        // IDs are permanent keys. Display order and preview indices may change independently.
        static readonly StageDefinition[] stages = {
            new StageDefinition(ShallowSeaId, "얕은 바다", 1, 0, true, "RabbitSurvivor"),
            new StageDefinition(1002, "바닷가", 2, 1, false, ""),
            new StageDefinition(1003, "농장", 3, 2, false, ""),
            new StageDefinition(1004, "심해", 4, 3, false, ""),
        };

        static StageCatalog()
        {
            System.Array.Sort(stages, (left, right) => left.DisplayOrder.CompareTo(right.DisplayOrder));
        }

        internal static int Count => stages.Length;
        internal static StageDefinition At(int displayIndex) => stages[displayIndex];

        internal static int IndexOf(int stageId)
        {
            for (int index = 0; index < stages.Length; index++)
                if (stages[index].Id == stageId)
                    return index;
            return -1;
        }

        internal static bool TryGet(int stageId, out StageDefinition stage)
        {
            foreach (StageDefinition candidate in stages) {
                if (candidate.Id == stageId) {
                    stage = candidate;
                    return true;
                }
            }
            stage = default;
            return false;
        }
    }
}
