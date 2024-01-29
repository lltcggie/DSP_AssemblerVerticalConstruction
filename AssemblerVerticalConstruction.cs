using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using crecheng.DSPModSave;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace AssemblerVerticalConstruction
{
    [BepInPlugin("lltcggie.DSP.plugin.AssemblerVerticalConstruction", "AssemblerVerticalConstruction", "1.1.2")]
    [BepInDependency(DSPModSavePlugin.MODGUID)]
    [ModSaveSettings(LoadOrder = LoadOrder.Postload)]
    public class AssemblerVerticalConstruction : BaseUnityPlugin, IModCanSave
    {
        public static readonly int CurrentSavedataVersion = 3;

        public static ConfigEntry<bool> IsResetNextIds;

        private static ManualLogSource _logger;
        public static new ManualLogSource Logger { get => _logger; }

        AssemblerVerticalConstruction()
        {
            _logger = base.Logger;
        }

        ~AssemblerVerticalConstruction()
        {
            if (IsResetNextIds.Value == true)
            {
                IsResetNextIds.Value = false;
                Config.Save();
            }
        }

        public void Awake()
        {
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

            IsResetNextIds = Config.Bind("config", "IsResetNextIds", false, "true if building overlay relationships must be recalculated when loading save data. This value is always reset to false when the game is closed.");
        }

        public void Import(BinaryReader binaryReader)
        {
            if (DSPGame.Game == null)
            {
                return;
            }

            if (IsResetNextIds.Value)
            {
                Logger.LogInfo("ResetNextIds");
                AssemblerPatches.ResetNextIds();
                return;
            }

            var version = binaryReader.ReadInt32() * -1; // 正数だとassemblerCapacityと誤認する恐れがあるので負数で扱う
            if (version < CurrentSavedataVersion)
            {
                Logger.LogInfo(string.Format("Old save data version: read {0} current {1}", version, CurrentSavedataVersion));
                Logger.LogInfo("ResetNextIds");
                AssemblerPatches.ResetNextIds();
                return;
            }
            else if (version != CurrentSavedataVersion)
            {
                Logger.LogWarning(string.Format("Invalid save data version: read {0} current {1}", version, CurrentSavedataVersion));
                Logger.LogInfo("ResetNextIds");
                AssemblerPatches.ResetNextIds();
                return;
            }

            var assemblerCapacity = binaryReader.ReadInt32();

            if (assemblerCapacity > AssemblerPatches.assemblerComponentEx.assemblerCapacity)
            {
                AssemblerPatches.assemblerComponentEx.SetAssemblerCapacity(assemblerCapacity);
            }

            for (int i = 0; i < assemblerCapacity; i++)
            {
                var num = binaryReader.ReadInt32();
                for (int j = 0; j < num; j++)
                {
                    var nextId = binaryReader.ReadInt32();
                    AssemblerPatches.assemblerComponentEx.SetAssemblerNext(i, j, nextId);
                }
            }
        }

        public void Export(BinaryWriter binaryWriter)
        {
            if (DSPGame.Game == null)
            {
                return;
            }

            binaryWriter.Write(CurrentSavedataVersion * -1); // 正数だとassemblerCapacityと誤認する恐れがあるので負数で扱う

            binaryWriter.Write(AssemblerPatches.assemblerComponentEx.assemblerCapacity);
            for (int i = 0; i < AssemblerPatches.assemblerComponentEx.assemblerCapacity; i++)
            {
                if (AssemblerPatches.assemblerComponentEx.assemblerNextIds[i] != null)
                {
                    binaryWriter.Write(AssemblerPatches.assemblerComponentEx.assemblerNextIds[i].Length);
                    for (int j = 0; j < AssemblerPatches.assemblerComponentEx.assemblerNextIds[i].Length; j++)
                    {
                        binaryWriter.Write(AssemblerPatches.assemblerComponentEx.assemblerNextIds[i][j]);
                    }
                }
                else
                {
                    binaryWriter.Write(0);
                }
            }
        }

        public void IntoOtherSave()
        {
            if (DSPGame.Game == null || DSPGame.IsMenuDemo) // タイトルのデモ用の工場ロードでも呼ばれるがこのMODの機能が使われているはずはないのでコスト削減のためにResetNextIds()しないようにする
            {
                return;
            }

            Logger.LogInfo("ResetNextIds");

            AssemblerPatches.ResetNextIds();
        }
    }

    [HarmonyPatch]
    internal class AssemblerPatches
    {
        class ModelSetting
        {
            public bool multiLevelAllowPortsOrSlots;
            public List<int> multiLevelAlternativeIds;
            public List<bool> multiLevelAlternativeYawTransposes;
            public Vector3 lapJoint;

            // 垂直建設研究の各レベルでの最大垂直個数
            // 最大時に惑星シールドをはみ出ないように設定しているつもり
            // [0]が初期値、[1]が垂直建設レベル1を研究し終えたとき、[6]が垂直建設レベル最大(6)まで研究し終えたとき
            public int[] multiLevelMaxBuildCount;

            public ModelSetting(bool multiLevelAllowPortsOrSlots, List<int> multiLevelAlternativeIds, List<bool> multiLevelAlternativeYawTransposes, Vector3 lapJoint, int[] multiLevelMaxBuildCount)
            {
                this.multiLevelAllowPortsOrSlots = multiLevelAllowPortsOrSlots;
                this.multiLevelAlternativeIds = multiLevelAlternativeIds;
                this.multiLevelAlternativeYawTransposes = multiLevelAlternativeYawTransposes;
                this.lapJoint = lapJoint;
                this.multiLevelMaxBuildCount = multiLevelMaxBuildCount;
            }
        }

        // 組立機の設定
        readonly static ModelSetting AssemblerSetting = new ModelSetting(
            false,
            new List<int> { 2303, 2304, 2305, 2318 },
            new List<bool> { false, false, false, false },
            new Vector3(0, 5.05f, 0),
            new int[7] { 2, 4, 6, 8, 10, 11, 12 }
            );
        // 製錬所の設定
        readonly static ModelSetting SmelterSetting = new ModelSetting(
            false,
            new List<int> { 2302, 2315, 2319 },
            new List<bool> { false, false, false },
            new Vector3(0, 4.3f, 0),
            new int[7] { 2, 4, 6, 8, 10, 12, 14 }
            );
        // 化学プラントの設定
        readonly static ModelSetting ChemicalPlantSetting = new ModelSetting(
            false,
            new List<int> { 2309, 2317 },
            new List<bool> { false, false },
            new Vector3(0, 6.85f, 0),
            new int[7] { 2, 4, 5, 6, 7, 8, 9 }
            );
        // 製油所の設定
        readonly static ModelSetting OilRefinerySetting = new ModelSetting(
            false,
            new List<int> { 2308 },
            new List<bool> { false },
            new Vector3(0, 11.8f, 0),
            new int[7] { 1, 2, 2, 3, 3, 4, 5 }
            );
        // 小型粒子衝突型加速器の設定
        readonly static ModelSetting MiniatureParticleColliderSetting = new ModelSetting(
            false,
            new List<int> { 2310 },
            new List<bool> { false },
            new Vector3(0, 15.2f, 0),
            new int[7] { 1, 2, 2, 3, 3, 3, 4 }
            );

        // ProtoIdと設定のマップ
        readonly static Dictionary<int, ModelSetting> ModelSettingDict = new Dictionary<int, ModelSetting>()
        {
            { 2303, AssemblerSetting }, // 組立機 Mk.I
            { 2304, AssemblerSetting }, // 組立機 Mk.II
            { 2305, AssemblerSetting }, // 組立機 Mk.III
            { 2318, AssemblerSetting }, // 再構成式組立機

            { 2302, SmelterSetting }, // アーク製錬所
            { 2315, SmelterSetting }, // プレーン製錬所
            { 2319, SmelterSetting }, // 負エントロピー製錬所

            { 2309, ChemicalPlantSetting }, // 化学プラント
            { 2317, ChemicalPlantSetting }, // 量子化学プラント

            { 2308, OilRefinerySetting }, // 製油所

            { 2310, MiniatureParticleColliderSetting }, // 小型粒子衝突型加速器
        };

        public static AssemblerComponentEx assemblerComponentEx = new AssemblerComponentEx();

        public static void ResetNextIds()
        {
            for (int i = 0; i < GameMain.data.factories.Length; i++)
            {
                if (GameMain.data.factories[i] == null)
                {
                    continue;
                }

                var _this = GameMain.data.factories[i].factorySystem;
                if (_this == null)
                {
                    continue;
                }

                var factoryIndex = _this.factory.index;
                int[] assemblerPrevIds = new int[assemblerComponentEx.assemblerNextIds[factoryIndex].Length];

                var assemblerCapacity = Traverse.Create(_this).Field("assemblerCapacity").GetValue<int>();
                for (int j = 1; j < assemblerCapacity; j++)
                {
                    var assemblerId = j;

                    int entityId = _this.assemblerPool[assemblerId].entityId;
                    if (entityId == 0)
                    {
                        continue;
                    }

                    int nextEntityId = entityId;
                    do
                    {
                        int prevEntityId = nextEntityId;

                        bool isOutput;
                        int otherObjId;
                        int otherSlot;
                        _this.factory.ReadObjectConn(nextEntityId, PlanetFactory.kMultiLevelOutputSlot, out isOutput, out otherObjId, out otherSlot);

                        nextEntityId = otherObjId;

                        if (nextEntityId > 0)
                        {
                            int prevAssemblerId = _this.factory.entityPool[prevEntityId].assemblerId;
                            int nextAssemblerId = _this.factory.entityPool[nextEntityId].assemblerId;
                            if (nextAssemblerId > 0 && _this.assemblerPool[nextAssemblerId].id == nextAssemblerId)
                            {
                                // まだRootは特定できないので0にしておく
                                // MEMO: まだRootが特定できないのでassemblerComponentEx.SetAssemblerInsertTarget()は呼び出せない
                                assemblerComponentEx.SetAssemblerNext(factoryIndex, prevAssemblerId, nextAssemblerId);
                                assemblerPrevIds[nextAssemblerId] = prevAssemblerId;
                            }
                        }
                    }
                    while (nextEntityId != 0);
                }

                // レシピの設定(一番下のアセンブラのレシピに合わせる)
                var lenAssemblerPrevIds = assemblerPrevIds.Length;
                for (int j = 1; j < lenAssemblerPrevIds; j++)
                {
                    var assemblerPrevId = assemblerPrevIds[j];
                    if (assemblerPrevId == 0 && _this.assemblerPool[assemblerPrevId].id == assemblerPrevId)
                    {
                        // Rootを見つけたらそこから子を辿ってレシピを設定する
                        var assemblerNextId = assemblerComponentEx.GetNextId(factoryIndex, j);
                        while (assemblerNextId != 0)
                        {
                            AssemblerComponentEx.FindRecipeIdForBuild(_this, assemblerNextId);
                            assemblerNextId = assemblerComponentEx.GetNextId(factoryIndex, assemblerNextId);
                        }
                    }
                }
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(ItemProto), "Preload")]
        private static void PreloadPatch(ItemProto __instance, int _index)
        {
            ModelProto modelProto = LDB.models.modelArray[__instance.ModelIndex];
            if (modelProto != null && modelProto.prefabDesc != null && modelProto.prefabDesc.isAssembler == true)
            {
                ModelSetting setting;
                if (ModelSettingDict.TryGetValue(__instance.ID, out setting))
                {
                    LDB.models.modelArray[__instance.ModelIndex].prefabDesc.multiLevel = true;
                    LDB.models.modelArray[__instance.ModelIndex].prefabDesc.multiLevelAllowPortsOrSlots = setting.multiLevelAllowPortsOrSlots;
                    LDB.models.modelArray[__instance.ModelIndex].prefabDesc.lapJoint = setting.lapJoint;

                    // multiLevelAlternative*に自身のIDは含まないので除く
                    var index = setting.multiLevelAlternativeIds.FindIndex(item => item == __instance.ID);
                    if (index >= 0)
                    {
                        var multiLevelAlternativeIds = new int[setting.multiLevelAlternativeIds.Count - 1];
                        var multiLevelAlternativeYawTransposes = new bool[setting.multiLevelAlternativeIds.Count - 1];

                        int count = 0;
                        for (int i = 0; i < setting.multiLevelAlternativeIds.Count; i++)
                        {
                            if (i == index)
                            {
                                continue;
                            }

                            multiLevelAlternativeIds[count] = setting.multiLevelAlternativeIds[i];
                            multiLevelAlternativeYawTransposes[count] = setting.multiLevelAlternativeYawTransposes[i];
                            count++;
                        }

                        LDB.models.modelArray[__instance.ModelIndex].prefabDesc.multiLevelAlternativeIds = multiLevelAlternativeIds;
                        LDB.models.modelArray[__instance.ModelIndex].prefabDesc.multiLevelAlternativeYawTransposes = multiLevelAlternativeYawTransposes;
                    }
                    else
                    {
                        LDB.models.modelArray[__instance.ModelIndex].prefabDesc.multiLevelAlternativeIds = setting.multiLevelAlternativeIds.ToArray();
                        LDB.models.modelArray[__instance.ModelIndex].prefabDesc.multiLevelAlternativeYawTransposes = setting.multiLevelAlternativeYawTransposes.ToArray();
                    }
                }
            }
        }

        [HarmonyPrefix, HarmonyPatch(typeof(FactorySystem), "SetAssemblerCapacity")]
        private static bool SetAssemblerCapacityPatch(FactorySystem __instance, int newCapacity)
        {
            var index = __instance.factory.index;
            if (index > assemblerComponentEx.assemblerNextIds.Length)
            {
                assemblerComponentEx.SetAssemblerCapacity(assemblerComponentEx.assemblerCapacity * 2);
            }

            var assemblerCapacity = Traverse.Create(__instance).Field("assemblerCapacity").GetValue<int>();

            int[] oldAssemblerNextIds = assemblerComponentEx.assemblerNextIds[index];
            assemblerComponentEx.assemblerNextIds[index] = new int[newCapacity];
            if (oldAssemblerNextIds != null)
            {
                Array.Copy(oldAssemblerNextIds, assemblerComponentEx.assemblerNextIds[index], (newCapacity <= assemblerCapacity) ? newCapacity : assemblerCapacity);
            }

            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PlanetFactory), "ApplyInsertTarget")]
        public static bool ApplyInsertTargetPatch(PlanetFactory __instance, int entityId, int insertTarget, int slotId, int offset)
        {
            if (entityId != 0)
            {
                if (insertTarget < 0)
                {
                    Assert.CannotBeReached();
                    insertTarget = 0;
                }
                else
                {
                    // MEMO: PlanetFactory.ApplyEntityOutput()から呼ばれるかPlanetFactory.ApplyEntityInput()からでentityIdとinsertTargetが入れ替わる
                    //       なのでどっちが上なのか下なのか判定しないといけない
                    //       このプログラムではinsertTargetが上(next)という想定
                    bool isOutput;
                    int otherObjId;
                    int otherSlot;
                    __instance.ReadObjectConn(entityId, PlanetFactory.kMultiLevelOutputSlot, out isOutput, out otherObjId, out otherSlot);
                    if (!(isOutput && otherObjId == insertTarget))
                    {
                        // Swap
                        int temp = insertTarget;
                        insertTarget = entityId;
                        entityId = temp;
                    }

                    int assemblerId = __instance.entityPool[entityId].assemblerId;
                    if (assemblerId > 0 && __instance.entityPool[insertTarget].assemblerId > 0)
                    {
                        assemblerComponentEx.SetAssemblerInsertTarget(__instance, assemblerId, insertTarget);
                    }
                }
            }
            return true;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(PlanetFactory), "ApplyEntityDisconnection")]
        public static bool ApplyEntityDisconnectionPatch(PlanetFactory __instance, int otherEntityId, int removingEntityId, int otherSlotId, int removingSlotId)
        {
            if (otherEntityId == 0)
            {
                return true;
            }

            var _this = __instance;
            int assemblerId = _this.entityPool[otherEntityId].assemblerId;
            if (assemblerId > 0)
            {
                int assemblerRemoveId = _this.entityPool[removingEntityId].assemblerId;
                if (assemblerRemoveId > 0)
                {
                    assemblerComponentEx.UnsetAssemblerInsertTarget(__instance, assemblerId, assemblerRemoveId);
                }
            }
            return true;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(PlanetFactory), "CreateEntityLogicComponents")]
        public static void CreateEntityLogicComponentsPatch(PlanetFactory __instance, int entityId, PrefabDesc desc, int prebuildId)
        {
            if (entityId == 0 || !desc.isAssembler)
            {
                return;
            }

            // プレビルド設置後にレシピ再設定
            // MEMO: プレビルドだった場合ApplyInsertTarget()後にレシピがプレビルドのものに上書きされてしまうのでここで再設定する必要がある
            int assemblerId = __instance.entityPool[entityId].assemblerId;
            AssemblerComponentEx.FindRecipeIdForBuild(__instance.factorySystem, assemblerId);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(FactorySystem), "GameTick", new Type[] { typeof(long), typeof(bool) })]
        public static void GameTickPatch(FactorySystem __instance, long time, bool isActive)
        {
            PerformanceMonitor.BeginSample(ECpuWorkEntry.Assembler);
            var factory = __instance.factory;
            var factoryIndex = factory.index;
            var assemblerPool = __instance.assemblerPool;
            var assemblerCursor = Traverse.Create(__instance).Field("assemblerCursor").GetValue<int>();
            for (int num17 = 1; num17 < assemblerCursor; num17++)
            {
                if (assemblerPool[num17].id == num17)
                {
                    var NextId = assemblerComponentEx.GetNextId(factoryIndex, num17);
                    if (NextId > 0)
                    {
                        assemblerComponentEx.UpdateOutputToNext(factory, factoryIndex, num17, assemblerPool, NextId, false);
                    }
                }
            }
            PerformanceMonitor.EndSample(ECpuWorkEntry.Assembler);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(FactorySystem), "GameTick", new Type[] { typeof(long), typeof(bool), typeof(int), typeof(int), typeof(int) })]
        public static void GameTickPatch(FactorySystem __instance, long time, bool isActive, int _usedThreadCnt, int _curThreadIdx, int _minimumMissionCnt)
        {
            var factory = __instance.factory;
            var factoryIndex = factory.index;
            var assemblerPool = __instance.assemblerPool;
            var assemblerCursor = Traverse.Create(__instance).Field("assemblerCursor").GetValue<int>();

            if (WorkerThreadExecutor.CalculateMissionIndex(1, assemblerCursor - 1, _usedThreadCnt, _curThreadIdx, _minimumMissionCnt, out var _start, out var _end))
            {
                for (int i = _start; i < _end; i++)
                {
                    bool useMutex = true;
                    if (i == 1 || i == assemblerCursor - 1 || (_start < i && i < _end - 1))
                    {
                        // 他のスレッドから触られる恐れがない箇所はMutexを使わなくても大丈夫なはず
                        useMutex = false;
                    }

                    var NextId = assemblerComponentEx.GetNextId(factoryIndex, i);
                    if (assemblerPool[i].id == i && NextId > 0)
                    {
                        assemblerComponentEx.UpdateOutputToNext(factory, factoryIndex, i, assemblerPool, NextId, useMutex);
                    }
                }
            }
        }

        public static void SyncAssemblerFunctions(FactorySystem factorySystem, Player player, int assemblerId)
        {
            var _this = factorySystem;
            int entityId = _this.assemblerPool[assemblerId].entityId;
            if (entityId == 0)
            {
                return;
            }

            int num = entityId;
            do
            {
                bool flag;
                int num3;
                int num4;
                _this.factory.ReadObjectConn(num, PlanetFactory.kMultiLevelInputSlot, out flag, out num3, out num4);
                num = num3;
                if (num > 0)
                {
                    int assemblerId2 = _this.factory.entityPool[num].assemblerId;
                    if (assemblerId2 > 0 && _this.assemblerPool[assemblerId2].id == assemblerId2)
                    {
                        if (_this.assemblerPool[assemblerId].recipeId > 0)
                        {
                            if (_this.assemblerPool[assemblerId2].recipeId != _this.assemblerPool[assemblerId].recipeId)
                            {
                                _this.TakeBackItems_Assembler(player, assemblerId2);
                                _this.assemblerPool[assemblerId2].SetRecipe(_this.assemblerPool[assemblerId].recipeId, _this.factory.entitySignPool);
                            }
                        }
                        else if (_this.assemblerPool[assemblerId2].recipeId != 0)
                        {
                            _this.TakeBackItems_Assembler(player, assemblerId2);
                            _this.assemblerPool[assemblerId2].SetRecipe(0, _this.factory.entitySignPool);
                        }
                    }
                }
            }
            while (num != 0);

            num = entityId;
            do
            {
                bool flag;
                int num3;
                int num4;
                _this.factory.ReadObjectConn(num, PlanetFactory.kMultiLevelOutputSlot, out flag, out num3, out num4);
                num = num3;
                if (num > 0)
                {
                    int assemblerId3 = _this.factory.entityPool[num].assemblerId;
                    if (assemblerId3 > 0 && _this.assemblerPool[assemblerId3].id == assemblerId3)
                    {
                        if (_this.assemblerPool[assemblerId].recipeId > 0)
                        {
                            if (_this.assemblerPool[assemblerId3].recipeId != _this.assemblerPool[assemblerId].recipeId)
                            {
                                _this.TakeBackItems_Assembler(_this.factory.gameData.mainPlayer, assemblerId3);
                                _this.assemblerPool[assemblerId3].SetRecipe(_this.assemblerPool[assemblerId].recipeId, _this.factory.entitySignPool);
                            }
                        }
                        else if (_this.assemblerPool[assemblerId3].recipeId != 0)
                        {
                            _this.TakeBackItems_Assembler(_this.factory.gameData.mainPlayer, assemblerId3);
                            _this.assemblerPool[assemblerId3].SetRecipe(0, _this.factory.entitySignPool);
                        }
                    }
                }
            }
            while (num != 0);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIAssemblerWindow), "OnRecipeResetClick")]
        public static void OnRecipeResetClickPatch(UIAssemblerWindow __instance)
        {
            if (__instance.assemblerId == 0 || __instance.factory == null)
            {
                return;
            }
            AssemblerComponent assemblerComponent = __instance.factorySystem.assemblerPool[__instance.assemblerId];
            if (assemblerComponent.id != __instance.assemblerId)
            {
                return;
            }
            SyncAssemblerFunctions(__instance.factorySystem, __instance.player, __instance.assemblerId);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(UIAssemblerWindow), "OnRecipePickerReturn")]
        public static void OnRecipePickerReturnPatch(UIAssemblerWindow __instance)
        {
            if (__instance.assemblerId == 0 || __instance.factory == null)
            {
                return;
            }
            AssemblerComponent assemblerComponent = __instance.factorySystem.assemblerPool[__instance.assemblerId];
            if (assemblerComponent.id != __instance.assemblerId)
            {
                return;
            }
            SyncAssemblerFunctions(__instance.factorySystem, __instance.player, __instance.assemblerId);
        }

        [HarmonyPostfix, HarmonyPatch(typeof(BuildingParameters), "PasteToFactoryObject")]
        public static void PasteToFactoryObjectPatch(BuildingParameters __instance, int objectId, PlanetFactory factory)
        {
            if (objectId <= 0)
            {
                return;
            }

            int assemblerId = factory.entityPool[objectId].assemblerId;
            if (assemblerId != 0 && __instance.type == BuildingType.Assembler && factory.factorySystem.assemblerPool[assemblerId].recipeId == __instance.recipeId)
            {
                ItemProto itemProto = LDB.items.Select((int)factory.entityPool[objectId].protoId);
                if (itemProto != null && itemProto.prefabDesc != null)
                {
                    SyncAssemblerFunctions(factory.factorySystem, factory.gameData.mainPlayer, assemblerId);
                }
            }

            return;
        }

        [HarmonyPostfix, HarmonyPatch(typeof(BuildTool_Click), "CheckBuildConditions")]
        public static void CheckBuildConditionsPatch(BuildTool_Click __instance, ref bool __result)
        {
            if (__instance.buildPreviews.Count == 0)
            {
                return;
            }

            GameHistoryData history = __instance.actionBuild.history;

            bool isNoLevelLimit = true;
            for (int i = 0; i < __instance.buildPreviews.Count; i++)
            {
                BuildPreview buildPreview = __instance.buildPreviews[i];
                if (buildPreview.condition != 0)
                {
                    continue;
                }

                if (buildPreview.desc.isAssembler && buildPreview.desc.multiLevel)
                {
                    int id = buildPreview.item.ID;

                    ModelSetting setting;
                    if (ModelSettingDict.TryGetValue(id, out setting))
                    {
                        var storageResearchLevel = history.storageLevel - 2;
                        if (storageResearchLevel < setting.multiLevelMaxBuildCount.Length) // 念のため垂直建設研究の最大レベルがMOD制作時の最大レベルである6より大きくなってたら何もしないようにする
                        {
                            int level = setting.multiLevelMaxBuildCount[storageResearchLevel];
                            int maxCount = setting.multiLevelMaxBuildCount[6];

                            int verticalCount = 0;
                            if (buildPreview.inputObjId != 0)
                            {
                                __instance.factory.ReadObjectConn(buildPreview.inputObjId, PlanetFactory.kMultiLevelInputSlot, out var isOutput, out var otherObjId, out var otherSlot);
                                while (otherObjId != 0)
                                {
                                    verticalCount++;
                                    __instance.factory.ReadObjectConn(otherObjId, PlanetFactory.kMultiLevelInputSlot, out isOutput, out otherObjId, out otherSlot);
                                }
                            }

                            if (level >= 2 && verticalCount >= level - 1)
                            {
                                isNoLevelLimit = level >= maxCount;
                                buildPreview.condition = EBuildCondition.OutOfVerticalConstructionHeight;
                                continue;
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < __instance.buildPreviews.Count; i++)
            {
                BuildPreview buildPreview3 = __instance.buildPreviews[i];
                if (buildPreview3.condition == EBuildCondition.OutOfVerticalConstructionHeight)
                {
                    __instance.actionBuild.model.cursorState = -1;
                    __instance.actionBuild.model.cursorText = buildPreview3.conditionText;
                    if (!isNoLevelLimit)
                    {
                        __instance.actionBuild.model.cursorText += "垂直建造可升级".Translate();
                    }

                    if (!VFInput.onGUI)
                    {
                        UICursor.SetCursor(ECursor.Ban);
                    }

                    __result = false;

                    break;
                }
            }
        }
    }
}
