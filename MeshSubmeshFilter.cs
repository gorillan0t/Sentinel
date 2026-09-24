using System.Collections.Generic;

internal static class MeshSubmeshFilter
{
    internal static int[][] FilterSubmeshes(float[] arg1, int[][] arg2)
    {
        if (arg1 != null && arg1.Length != 0 && arg2 != null)
        {
            int[][] array = new int[arg2.Length][];
            int     num   = 0;
            int     num2  = 0;
            for (int i = 0; i < arg2.Length; i++)
            {
                int[] array2 = arg2[i];
                if (array2 != null && array2.Length % 3 == 0)
                {
                    List<int> list = new();
                    num2 += array2.Length;
                    for (int j = 0; j < array2.Length; j += 3)
                    {
                        int num3 = array2[j];
                        int num4 = array2[j + 1];
                        int num5 = array2[j + 2];
                        if ((uint)num3 < arg1.Length && (uint)num4 < arg1.Length && (uint)num5 < arg1.Length)
                        {
                            if (!(arg1[num3] < 0.5f) && !(arg1[num4] < 0.5f) && !(arg1[num5] < 0.5f) && !float.IsNaN(arg1[num3]) && !float.IsNaN(arg1[num4]) && !float.IsNaN(arg1[num5]))
                            {
                                list.Add(num3);
                                list.Add(num4);
                                list.Add(num5);
                            }

                            continue;
                        }

                        return null;
                    }

                    array[i] =  list.ToArray();
                    num      += list.Count;

                    continue;
                }

                return null;
            }

            if (num > 0 && num < num2)
            {
                return array;
            }

            return null;
        }

        return null;
    }
}