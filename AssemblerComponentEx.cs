﻿using System;

namespace AssemblerVerticalConstruction
{
    public class AssemblerComponentEx
    {
        public int[][] assemblerNextIds = new int[64 * 6][]; // 上に重なってるのがNext
        public int[][] assemblerRootIds = new int[64 * 6][]; // 地面に接してるのがRoot(地面に接してる場合は0)
        public int assemblerCapacity = 64 * 6;
        public int transfarCount = 6; // 1チックで何個出力を受け渡すか

        public void SetAssemblerCapacity(int newCapacity)
        {
            var oldAssemblerNextIds = this.assemblerNextIds;
            var oldAssemblerRootIds = this.assemblerRootIds;

            this.assemblerNextIds = new int[newCapacity][];
            this.assemblerRootIds = new int[newCapacity][];

            if (oldAssemblerNextIds != null)
            {
                Array.Copy(oldAssemblerNextIds, this.assemblerNextIds, (newCapacity <= this.assemblerCapacity) ? newCapacity : assemblerCapacity);
            }

            if (oldAssemblerRootIds != null)
            {
                Array.Copy(oldAssemblerRootIds, this.assemblerRootIds, (newCapacity <= this.assemblerCapacity) ? newCapacity : assemblerCapacity);
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

        public int GetRootId(int index, int assemblerId)
        {
            if (index >= assemblerRootIds.Length)
            {
                return 0;
            }

            if (this.assemblerRootIds[index] == null || assemblerId >= this.assemblerRootIds[index].Length)
            {
                return 0;
            }

            return this.assemblerRootIds[index][assemblerId];
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

                    // つながってるということはRootは同じはず
                    var assemblerRootId = this.assemblerRootIds[index][assemblerId];
                    if (assemblerRootId == 0)
                    {
                        // assemblerRootIdが0ということはassemblerIdがRootなのでassemblerIdを使う
                        assemblerRootId = assemblerId;
                    }

                    this.assemblerRootIds[index][nextAssemblerId] = assemblerRootId;

                    // Rootと同じレシピにする
                    if (nextAssemblerId != 0 && assemblerRootId != 0 && __instance.factorySystem.assemblerPool[assemblerRootId].recipeId != __instance.factorySystem.assemblerPool[nextAssemblerId].recipeId)
                    {
                        __instance.factorySystem.assemblerPool[nextAssemblerId].SetRecipe(__instance.factorySystem.assemblerPool[assemblerRootId].recipeId, __instance.factorySystem.factory.entitySignPool);
                    }
                }
            }
        }

        public void SetAssemblerRecipe(PlanetFactory __instance, int assemblerId)
        {
            if (assemblerId <= 0)
            {
                return;
            }

            var index = __instance.factorySystem.factory.index;

            var assemblerRootId = this.assemblerRootIds[index][assemblerId];
            if (assemblerRootId == 0)
            {
                return;
            }

            // Rootと同じレシピにする
            if (__instance.factorySystem.assemblerPool[assemblerRootId].recipeId != __instance.factorySystem.assemblerPool[assemblerId].recipeId)
            {
                __instance.factorySystem.assemblerPool[assemblerId].SetRecipe(__instance.factorySystem.assemblerPool[assemblerRootId].recipeId, __instance.factorySystem.factory.entitySignPool);
            }
        }

        public void UnsetAssemblerInsertTarget(PlanetFactory __instance, int assemblerId, int assemblerRemoveId)
        {
            var index = __instance.factorySystem.factory.index;
            if (assemblerId != 0 && __instance.factorySystem.assemblerPool[assemblerId].id == assemblerId)
            {
                this.assemblerNextIds[index][assemblerId] = 0;
                this.assemblerRootIds[index][assemblerRemoveId] = 0;
            }
        }

        public void SetAssemblerNextAndRootId(int index, int assemblerId, int nextId, int rootId)
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

            if (assemblerRootIds[index] == null || assemblerId >= assemblerRootIds[index].Length)
            {
                var array = this.assemblerRootIds[index];

                var newCapacity = assemblerId * 2;
                newCapacity = newCapacity > 256 ? newCapacity : 256;
                this.assemblerRootIds[index] = new int[newCapacity];
                if (array != null)
                {
                    var len = array.Length;
                    Array.Copy(array, this.assemblerRootIds[index], (newCapacity <= len) ? newCapacity : len);
                }
            }

            this.assemblerNextIds[index][assemblerId] = nextId;
            this.assemblerRootIds[index][assemblerId] = rootId;
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
