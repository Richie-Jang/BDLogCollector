﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LogFileCollector
{
    public partial class Form2 : Form
    {
        private void updateTextBoxByResult(bool res, TextBox tb)
        {
            if (res)
            {
                tb.BackColor = Color.LightYellow;
                tb.ForeColor = Color.Black;
                tb.Text = "OKAY";
            } else
            {
                tb.BackColor = Color.LightPink;
                tb.ForeColor = Color.Red;               
                tb.Text = "NG";
            }
        }

        public Form2(bool[] results, Tuple<double, double, double> checkRules, string resultLogPath)
        {
            InitializeComponent();

            lb_c1.Text = $"{checkRules.Item1:F1}%";
            lb_c2.Text = $"{checkRules.Item2:F1}Std";
            lb_c3.Text = $"{checkRules.Item3:F1}%";

            updateTextBoxByResult(results[0], tf_r1);
            updateTextBoxByResult(results[1], tf_r2);
            updateTextBoxByResult(results[2], tf_r3);

            lb_info.Text = $"Detail : {resultLogPath}\nIf Your result is NG";
        }
    }
}
