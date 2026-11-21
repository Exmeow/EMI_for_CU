using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace EMI
{
    /// <summary>从 QoL 实际生成的配方行读取身份，仅在插件已加载时接入。</summary>
    internal static class QolRecipeRows
    {
        internal const string PluginGuid = "org.bepinex.plugins.qol_unknown";
        internal const string BulkOwner = "org.bepinex.plugins.qol.bulkcrafting";
        internal const string FilterOwner = "org.bepinex.plugins.qol.misc.recipefilter";

        private static bool _hasHooks;

        [ThreadStatic]
        private static Capture _current;

        internal sealed class Capture : IDisposable
        {
            private readonly Capture _previous = _current;
            internal readonly Dictionary<GameObject, Recipe> Recipes = new Dictionary<GameObject, Recipe>();

            public void Dispose()
            {
                _current = _previous;
            }
        }

        internal static Capture BeginCapture()
        {
            return _hasHooks ? _current = new Capture() : null;
        }

        internal static void Install(Harmony harmony, Assembly qolAssembly)
        {
            if (qolAssembly == null)
            {
                return;
            }

            _hasHooks = InstallHook(harmony, qolAssembly, "QoL_Unknown.CraftingModule", "SetUpRecipeRow",
                new[] { typeof(PlayerCamera), typeof(GameObject), typeof(Recipe), typeof(int), typeof(Color), typeof(Color) },
                postfix: nameof(BulkRowPostfix));
            _hasHooks |= InstallHook(harmony, qolAssembly, "QoL_Unknown.RecipeFilterPatch", "RebuildRecipeList",
                new[] { typeof(PlayerCamera), typeof(Item) }, transpiler: nameof(FilterRowsTranspiler));
        }

        private static bool InstallHook(
            Harmony harmony, Assembly assembly, string typeName, string methodName, Type[] parameters,
            string postfix = null, string transpiler = null)
        {
            try
            {
                MethodInfo target = assembly.GetType(typeName)?.GetMethod(methodName,
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                    null, parameters, null);
                if (target == null || target.ReturnType != typeof(void))
                {
                    throw new MissingMethodException(typeName, methodName);
                }

                harmony.Patch(target,
                    postfix: postfix == null ? null : new HarmonyMethod(typeof(QolRecipeRows), postfix),
                    transpiler: transpiler == null ? null : new HarmonyMethod(typeof(QolRecipeRows), transpiler));
                return true;
            }
            catch (Exception exception)
            {
                EmiPlugin.Log?.LogWarning($"[EMI] QoL recipe row hook unavailable ({typeName}.{methodName}): {exception.Message}");
                return false;
            }
        }

        private static void BulkRowPostfix(GameObject __1, Recipe __2)
        {
            RecordRow(__1, __2);
        }

        private static void RecordRow(GameObject row, Recipe recipe)
        {
            if (_current != null && row != null && recipe != null)
            {
                _current.Recipes[row] = recipe;
            }
        }

        internal static IEnumerable<CodeInstruction> FilterRowsTranspiler(
            IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            List<CodeInstruction> code = instructions.ToList();
            MethodInfo addRow = AccessTools.Method(typeof(List<GameObject>), nameof(List<GameObject>.Add));
            MethodInfo description = AccessTools.PropertyGetter(typeof(Recipe), nameof(Recipe.description));
            FieldInfo tooltipDescription = AccessTools.Field(typeof(UITooltip), nameof(UITooltip.tipDesc));
            int addIndex = code.FindIndex(instruction => instruction.Calls(addRow));
            IList<LocalVariableInfo> locals = __originalMethod.GetMethodBody().LocalVariables;

            // QoL 1.1.5 设置当前行的提示说明后，立即把该行加入配方列表。
            // 只在识别到完整的指令序列时记录当前行和配方。
            if (addIndex < 5 || addIndex != code.FindLastIndex(instruction => instruction.Calls(addRow)) ||
                !LoadsLocalOfType(code[addIndex - 5], locals, typeof(Recipe)) ||
                !code[addIndex - 4].Calls(description) ||
                code[addIndex - 3].opcode != OpCodes.Stfld || !Equals(code[addIndex - 3].operand, tooltipDescription) ||
                !LoadsLocalOfType(code[addIndex - 2], locals, typeof(List<GameObject>)) ||
                !LoadsLocalOfType(code[addIndex - 1], locals, typeof(GameObject)))
            {
                EmiPlugin.Log?.LogWarning("[EMI] QoL recipe filter row layout not recognized. EMI highlights will skip unmapped rows.");
                return code;
            }

            CodeInstruction row = code[addIndex - 1];
            CodeInstruction recipe = code[addIndex - 5];
            code.InsertRange(addIndex + 1, new[]
            {
                new CodeInstruction(row.opcode, row.operand),
                new CodeInstruction(recipe.opcode, recipe.operand),
                CodeInstruction.Call(typeof(QolRecipeRows), nameof(RecordRow))
            });
            return code;
        }

        private static bool LoadsLocalOfType(CodeInstruction instruction, IList<LocalVariableInfo> locals, Type type)
        {
            int index;
            if (instruction.opcode == OpCodes.Ldloc_0) index = 0;
            else if (instruction.opcode == OpCodes.Ldloc_1) index = 1;
            else if (instruction.opcode == OpCodes.Ldloc_2) index = 2;
            else if (instruction.opcode == OpCodes.Ldloc_3) index = 3;
            else if (instruction.opcode == OpCodes.Ldloc || instruction.opcode == OpCodes.Ldloc_S)
            {
                index = instruction.operand is LocalVariableInfo local
                    ? local.LocalIndex
                    : Convert.ToInt32(instruction.operand);
            }
            else return false;

            return index >= 0 && index < locals.Count && locals[index].LocalType == type;
        }
    }
}
