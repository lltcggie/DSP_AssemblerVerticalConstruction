using System;

namespace AssemblerVerticalConstruction
{
    public class AssemblerComponentEx
    {
        public int[][] assemblerNextIds = new int[64 * 6][]; // 上に重なってるのがNext
        public int assemblerCapacity = 64 * 6;
        public int transfarCount = 6; // 1チックで何個出力を受け渡すか

        public void SetAssemblerCapacity(int newCapacity)
        {
            var oldAssemblerNextIds = this.assemblerNextIds;

            this.assemblerNextIds = new int[newCapacity][];

            if (oldAssemblerNextIds != null)
            {
                Array.Copy(oldAssemblerNextIds, this.assemblerNextIds, (newCapacity <= this.assemblerCapacity) ? newCapacity : assemblerCapacity);
            }

            this.assemblerCapacity = newCapacity;
        }

        public int GetNextId(int index, int assemblerId)
        {
            if (index >= assemblerNextIds.Length)
            {
                return 0;
            }

            if (this.assemblerNextIds[index] == null || assemblerId >= this.assemblerNextIds[index].Length)
            {
                return 0;
            }

            return this.assemblerNextIds[index][assemblerId];
        }

        public static void FindRecipeIdForBuild(FactorySystem factorySystem, int assemblerId)
        {
            // 上下のアセンブラからレシピを設定
            // LabComponent.FindLabFunctionsForBuild()を参考に実装
            // 自身から下、
            var _this = factorySystem;
            int entityId = _this.assemblerPool[assemblerId].entityId;
            if (entityId == 0)
            {
                return;
            }

            bool isOutput;
            int otherObjId;
            int otherSlot;

            // まずは自身から下へ辿っていく
            int objId = entityId;
            do
            {

                _this.factory.ReadObjectConn(objId, PlanetFactory.kMultiLevelInputSlot, out isOutput, out otherObjId, out otherSlot);
                objId = otherObjId;
                if (objId > 0)
                {
                    int assemblerId2 = _this.factory.entityPool[objId].assemblerId;
                    if (assemblerId2 > 0 && _this.assemblerPool[assemblerId2].id == assemblerId2)
                    {
                        if (_this.assemblerPool[assemblerId2].recipeId > 0)
                        {
                            _this.assemblerPool[assemblerId].SetRecipe(_this.assemblerPool[assemblerId2].recipeId, _this.factory.entitySignPool);
                            return;
                        }
                    }
                }
            }
            while (objId != 0);

            // 駄目だったら自身から上へ辿っていく
            objId = entityId;
            do
            {
                _this.factory.ReadObjectConn(objId, PlanetFactory.kMultiLevelInputSlot, out isOutput, out otherObjId, out otherSlot);
                objId = otherObjId;
                if (objId > 0)
                {
                    int assemblerId3 = _this.factory.entityPool[objId].assemblerId;
                    if (assemblerId3 > 0 && _this.assemblerPool[assemblerId3].id == assemblerId3)
                    {
                        if (_this.assemblerPool[assemblerId3].recipeId > 0)
                        {
                            _this.assemblerPool[assemblerId].SetRecipe(_this.assemblerPool[assemblerId3].recipeId, _this.factory.entitySignPool);
                            return;
                        }
                    }
                }
            }
            while (objId != 0);
        }

        public void SetAssemblerInsertTarget(PlanetFactory __instance, int assemblerId, int nextEntityId)
        {
            var index = __instance.factorySystem.factory.index;
            if (index >= assemblerNextIds.Length)
            {
                this.SetAssemblerCapacity(this.assemblerCapacity * 2);
            }

            if (assemblerId != 0 && __instance.factorySystem.assemblerPool[assemblerId].id == assemblerId)
            {
                if (nextEntityId == 0)
                {
                    this.assemblerNextIds[index][assemblerId] = 0;
                }
                else
                {
                    var nextAssemblerId = __instance.entityPool[nextEntityId].assemblerId;

                    this.assemblerNextIds[index][assemblerId] = nextAssemblerId;

                    // 同じレシピにする
                    FindRecipeIdForBuild(__instance.factorySystem, assemblerId);
                }
            }
        }

        public void UnsetAssemblerInsertTarget(PlanetFactory __instance, int assemblerId, int assemblerRemoveId)
        {
            var index = __instance.factorySystem.factory.index;
            if (assemblerId != 0 && __instance.factorySystem.assemblerPool[assemblerId].id == assemblerId)
            {
                this.assemblerNextIds[index][assemblerId] = 0;
            }
        }

        public void SetAssemblerNext(int index, int assemblerId, int nextId)
        {
            if (index >= assemblerNextIds.Length)
            {
                this.SetAssemblerCapacity(this.assemblerCapacity * 2);
            }

            if (assemblerNextIds[index] == null || assemblerId >= assemblerNextIds[index].Length)
            {
                var array = this.assemblerNextIds[index];

                var newCapacity = assemblerId * 2;
                newCapacity = newCapacity > 256 ? newCapacity : 256;
                this.assemblerNextIds[index] = new int[newCapacity];
                if (array != null)
                {
                    var len = array.Length;
                    Array.Copy(array, this.assemblerNextIds[index], (newCapacity <= len) ? newCapacity : len);
                }
            }

            this.assemblerNextIds[index][assemblerId] = nextId;
        }

        public void UpdateOutputToNext(FactorySystem __instance, int planeIndex, int assemblerId, AssemblerComponent[] assemblerPool, bool useMutex)
        {
            if (planeIndex >= assemblerNextIds.Length || assemblerNextIds[planeIndex] == null || assemblerId >= assemblerNextIds[planeIndex].Length || assemblerId >= assemblerPool.Length)
            {
                return;
            }

            var assemblerNextId = assemblerNextIds[planeIndex][assemblerId];
            if (assemblerNextId >= assemblerPool.Length)
            {
                return;
            }

            if (assemblerPool[assemblerNextId].id == 0 || assemblerPool[assemblerNextId].id != assemblerNextId)
            {
                Assert.CannotBeReached();
                this.assemblerNextIds[planeIndex][assemblerId] = 0;
            }

            if (assemblerPool[assemblerNextId].needs != null && assemblerPool[assemblerId].recipeId == assemblerPool[assemblerNextId].recipeId)
            {
                if (useMutex)
                {
                    var entityId = assemblerPool[assemblerId].entityId;
                    var entityNextId = assemblerPool[assemblerNextId].entityId;

                    lock (__instance.factory.entityMutexs[entityId])
                    {
                        lock (__instance.factory.entityMutexs[entityNextId])
                        {
                            UpdateOutputToNextInner(assemblerId, assemblerNextId, assemblerPool);
                        }
                    }
                }
                else
                {
                    UpdateOutputToNextInner(assemblerId, assemblerNextId, assemblerPool);
                }
            }
        }

        private void UpdateOutputToNextInner(int assemblerId, int assemblerNextId, AssemblerComponent[] assemblerPool)
        {
            var _this = assemblerPool[assemblerId];
            if (_this.served != null && assemblerPool[assemblerNextId].served != null && assemblerPool[assemblerNextId].recipeId == _this.recipeId)
            {
                int num = _this.served.Length;
                for (int i = 0; i < num; i++)
                {
                    int needs = assemblerPool[assemblerNextId].needs[i];
                    int requireCount = assemblerPool[assemblerNextId].requireCounts[i];
                    int served = _this.served[i];
                    if (needs > 0 && served > requireCount)
                    {
                        // assemblerIdに一回製造分より多い在庫があったらneedsを満たすように余りをsemblerNextIdへ送る
                        int transfar = Math.Min(served - requireCount, needs);

                        if (_this.incServed[i] <= 0)
                        {
                            _this.incServed[i] = 0;
                        }

                        //var args = new object[] { _this.served[i], _this.incServed[i], transfar };
                        //int out_one_inc_level = Traverse.Create(assemblerPool[assemblerNextId]).Method("split_inc_level", new System.Type[] { typeof(int).MakeByRefType(), typeof(int).MakeByRefType(), typeof(int) }).GetValue<int>(args);
                        //_this.served[i] = (int)args[0];
                        //_this.incServed[i] = (int)args[1];

                        // MEMO: 本当はassemblerPool[assemblerNextId].split_inc_level()を呼ぶのが正しい。
                        //       が、split_inc_level()はstaticでいいのにstaticになってない、さらにprivateなのでここから呼び出すのにどうしてもコストがかかる。
                        //       なのでsplit_inc_level()の実装をそのまま持ってくることにした。
                        int out_one_inc_level = split_inc_level(ref _this.served[i], ref _this.incServed[i], transfar);
                        if (_this.served[i] == 0)
                        {
                            _this.incServed[i] = 0;
                        }

                        assemblerPool[assemblerNextId].served[i] += transfar;
                        assemblerPool[assemblerNextId].incServed[i] += transfar * out_one_inc_level;
                    }
                }
            }

            if (_this.produced != null && assemblerPool[assemblerNextId].produced != null)
            {
                for (int l = 0; l < _this.productCounts.Length; l++)
                {
                    var maxCount = _this.productCounts[l] * 9;
                    if (_this.produced[l] < maxCount && assemblerPool[assemblerNextId].produced[l] > 0)
                    {
                        var count = Math.Min(transfarCount, assemblerPool[assemblerNextId].produced[l]);
                        _this.produced[l] += count;
                        assemblerPool[assemblerNextId].produced[l] -= count;
                    }
                }
            }
        }

        // AssemblerComponent.split_inc_level()がオリジナル
        private static int split_inc_level(ref int n, ref int m, int p)
        {
            int num = m / n;
            int num2 = m - num * n;
            n -= p;
            num2 -= n;
            m -= ((num2 > 0) ? (num * p + num2) : (num * p));
            return num;
        }
    }
}
