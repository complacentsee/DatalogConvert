﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace libFTH
{
    public struct PITIMESTAMP : IFormattable
    {
        public int month;
        public int year;
        public int day;
        public int hour;
        public int minute;
        public int tzinfo;
        public double second;

        public PITIMESTAMP(DateTime dt)
        {
            month = dt.Month;
            year = dt.Year;
            day = dt.Day;
            hour = dt.Hour;
            minute = dt.Minute;
            second = dt.Second + dt.Millisecond / 1000;
            tzinfo = 0;
        }

        public string ToString(string format, IFormatProvider provider)
        {
            return String.Concat($"{hour}:{minute}:{second}");
        }
    }

    public struct FloatSnapshot
    {
        public int ptid;
        public double val;
        public DateTime dt;
    }

    public class PIAPI
    {
        public static string POINT_PREFIX = "";
        public static string TagToPoint(string tagname)
        {
            string sanitizedTagname = Regex.Replace(tagname, @"[^a-zA-Z0-9_%:]|[\x00-\x1F]", ".");
            sanitizedTagname = Regex.Replace(sanitizedTagname, @"^[^a-zA-Z0-9_%]+", "");
            if (string.IsNullOrEmpty(sanitizedTagname))
            {
                throw new ArgumentException($"Tag name '{tagname}' must contain at least one valid starting character");
            }
            return POINT_PREFIX + sanitizedTagname;
        }

        private const string PIAPIDLL = "piapi.dll"; // 64bit!

        [DllImport(PIAPIDLL)]
        static extern Int32 piut_setservernode(string name);
        [DllImport(PIAPIDLL)]
        static extern Int32 pipt_findpoint(string name, out Int32 pointNumber);
        [DllImport(PIAPIDLL)]
        static extern Int32 pisn_putsnapshotx(Int32 ptnum, ref double drval, ref Int32 ival, byte[] bval, ref UInt32 bsize,
                                              ref Int32 istat, ref Int16 flags, ref PITIMESTAMP timestamp);
        [DllImport(PIAPIDLL)]
        static extern Int32 pisn_putsnapshotsx(Int32 count, Int32[] ptnum, double[] drval, Int32[] ival, byte[,] bval,
                                               UInt32[] bsize, Int32[] istat, Int16[] flags, PITIMESTAMP[] timestamp,
                                               Int32[] errors);

        public static bool Connect(string serverName)
        {
            Int32 err = piut_setservernode(serverName);
            if (err != 0)
            {
                throw new Exception($"piut_setservernode returned error {err}");
            }
            return true;
        }

        public static Int32 GetPointNumber(string ptName)
        {
            if (ptName.Length > 80)
            {
                throw new Exception($"historian point name {ptName} > 80 characters not supported");
            }
            Int32 pointNumber;
            int err = pipt_findpoint(ptName, out pointNumber);
            if (err != 0)
            {
                throw new Exception($"error finding historian point {ptName}, pipt_findpoint returned error {err}");
            }
            return pointNumber;
        }

        public static bool PutSnapshot(int ptId, double v, DateTime dt)
        {
            Int32 ival = 0;
            UInt32 bsize = 0;
            Int32 istat = 0;
            Int16 flags = 0;
            PITIMESTAMP ts = new PITIMESTAMP(dt);

            Int32 err = pisn_putsnapshotx(ptId, ref v, ref ival, null, ref bsize, ref istat, ref flags, ref ts);
            if (err != 0)
            {
                throw new Exception($"pisn_putsnapshotx returned error {err}");
            }
            return true;
        }

        public static bool PutSnapshots(Int32 count, Int32[] ptids, double[] vs, PITIMESTAMP[] ts)
        {
            Int32[] ivals = new Int32[count];
            UInt32[] bsizes = new UInt32[count];
            Int32[] istats = new Int32[count];
            Int16[] flags = new Int16[count];

            Int32[] errors = new Int32[count];

            Int32 err = pisn_putsnapshotsx(count, ptids, vs, ivals, null, bsizes, istats, flags, ts, errors);
            if (err != 0)
            {
                int arr_item = 0;
                int arr_err = 0;
                for (int i = 0; i < count; i++)
                {
                    if (errors[i] != 0)
                    {
                        arr_item = i;
                        arr_err = errors[i];
                    }
                }
                throw new Exception($"pisn_putsnapshotsx returned error {err}, item {arr_item}, point number {ptids[arr_item]}, ts {ts[arr_item]}, value {vs[arr_item]}, err {arr_err}");
            }
            return true;
        }
    }
}
