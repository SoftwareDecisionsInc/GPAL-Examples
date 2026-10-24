// =============================================================================
// GPAL - Generally Positive Automation Library
// Copyright © 2026 Software Decisions, Inc. All rights reserved.
//
// This file is part of GPAL.
// Licensed under the Business Source License 1.1
//
// Primary development, architecture, and vision by Michael B. Vederman,
// CEO of Software Decisions, Inc., Texas.
//
// Internal development maintained privately.
// Public releases appear on GitHub: https://github.com/SoftwareDecisionsInc/GPAL.
//
// See LICENSE for full terms, including Additional Use Grant.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using GenerallyPositive;
using GenerallyPositive.GPALForm;
using static GenerallyPositive.Enums;

namespace OpenAPITester
{
    // loads an OpenAPI map, calls one of its endpoints with GET and then OPTIONS, and pretty-prints each answer.
    // from a form, and everything GPAL publishes goes to the Log tab
    internal class Program
    {
        static bool running { get; set; }

        // set by Stop: no request starts after it
        static bool stopRequested { get; set; }

        // ── Run tab ───────────────────────────────────────────────────────────

        static GPALTab runTab = GPAL.Tab.WithName("runTab").WithText("Run").ToGPALObject();

        static GPALTextArea aboutBlurb = (GPALTextArea)GPAL.TextArea
            .WithReadOnly(true)
            .WithHeight(130)
            .WithName("aboutBlurb")
            .WithText("Tests the REST client's OpenAPI support. It loads the basicstore OpenAPI map from testpages.eviltester.com, lists its endpoints, then calls /api/basicstore/products with GET and again with OPTIONS, and pretty-prints each answer with the Converter. It reads only from that site and writes no files." + Environment.NewLine + Environment.NewLine +
                      "The Log tab has everything GPAL says, including both pretty-printed answers.")
            .ToGPALObject();

        // GPAL's own tracing
        static GPALCheckbox debugCheck = GPAL.CheckboxFor("Include DEBUG in the log", true);

        static GPALButton runBtn = GPAL.Button
            .WithName("run")
            .WithText("▶ Run")
            .WithCallback<EventHandler>((s, e) => runBtn.Enabled = false)
            .WithCallbackOffUIThread<EventHandler>(OnRun)
            .ToGPALObject();

        static GPALButton stopBtn = GPAL.Button
            .WithEnabled(false)
            .WithName("stop")
            .WithText("⏹ Stop")
            .WithCallback<EventHandler>(OnStop)
            .ToGPALObject();

        static GPALButton exitBtn = GPAL.Button
            .WithName("exit")
            .WithText("🚪 Exit")
            .WithCallback<EventHandler>((s, e) => System.Windows.Forms.Application.Exit())
            .ToGPALObject();

        // ── Log tab ───────────────────────────────────────────────────────────

        static GPALTab logTab = GPAL.Tab.WithName("logTab").WithText("Log").ToGPALObject();

        // Every event GPAL publishes. The status strip shows only the current step.
        static GPALTextArea logArea = (GPALTextArea)GPAL.TextArea.WithReadOnly(true).WithHeight(420).WithName("log");

        // Every log line since the last Clear Log, for Save Log. The Log box keeps only the last 2000, so a long run
        // loses its start there.
        static List<string> fullLog = new List<string>();

        static GPALButton clearLogBtn = GPAL.Button
            .WithName("clearLog")
            .WithText("🧹 Clear Log")
            .WithCallback<EventHandler>(OnClearLog)
            .ToGPALObject();

        // writes every line since the last Clear Log, not only the 2000 the box is showing
        static GPALButton saveLogBtn = GPAL.Button
            .WithName("saveLog")
            .WithText("💾 Save Log")
            .WithCallback<EventHandler>(OnSaveLog)
            .ToGPALObject();

        static GPALStatusStrip statusBar = GPAL.StatusStrip
            .WithText("Ready")
            .WithName("status")
            .ToGPALObject();

        [STAThread]
        static void Main(string[] args)
        {
            // DEBUG and DEEPDEBUG are GPAL's internal tracing; leave them out.
            GPAL.WithHandlerEvents(GPALEventType.All & ~(GPALEventType.DEBUG | GPALEventType.DEEPDEBUG))
                .WithExceptionHandler(OnGPALEvent)
                .WithInformationHandler(OnGPALEvent);

            var form = GPAL.Form
                .WithTitle("OpenAPITester  |  OpenAPI map and REST calls")
                .WithWidth(760)
                .WithHeight(640)
                .WithFormControl(statusBar)

                .WithFormControl(runTab)
                .WithFormControl(aboutBlurb)
                .WithFormControl(debugCheck)
                .WithFormControl(GPAL.SpacerFor(18))
                .WithFormControl(runBtn)
                .WithFormControl(stopBtn)
                .WithFormControl(GPAL.SpacerFor(18))
                .WithFormControl(exitBtn)

                .WithFormControl(logTab)
                .WithFormControl(clearLogBtn)
                .WithFormControl(saveLogBtn)
                .WithFormControl(logArea)

                .ToGPALObject();

            form.ShowDialog();
        }

        static void OnRun(object s, EventArgs e)
        {
            if (true == running)
                return;

            running = true;
            stopRequested = false;
            runBtn.Enabled = false;
            stopBtn.Enabled = true;

            // DEEPDEBUG is always left out; its raw protocol traffic buries everything else.
            GPAL.WithHandlerEvents(true == debugCheck.Checked
                ? GPALEventType.All & ~GPALEventType.DEEPDEBUG
                : GPALEventType.All & ~(GPALEventType.DEBUG | GPALEventType.DEEPDEBUG));

            try
            {
                RunTest();
            }
            catch (Exception ex)
            {
                // The form stays usable after a run that throws.
                GPAL.PublishSimpleEvent(GPALEventType.EXCEPTION, "The run ended in an exception", ex: ex);
            }
            finally
            {
                running = false;
                stopBtn.Enabled = false;
                runBtn.Enabled = true;
                SetStatus(true == stopRequested
                    ? "Stopped. The Log tab has what ran."
                    : "Done. The answers are on the Log tab.");
            }
        }

        // no request starts after this; the one running finishes
        static void OnStop(object s, EventArgs e)
        {
            stopRequested = true;
            stopBtn.Enabled = false;

            SetStatus("Stopping after the request that is running now...");
            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, "Stop pressed, starting no more requests");
        }

        static void RunTest()
        {
            SetStatus("Loading the OpenAPI map...");

            IRESTClient restClient = GPAL.RESTClient.LoadOpenAPIMap(@"https://testpages.eviltester.com/openapi/basicstore-api-v2.yaml").ListOpenAPIEndPoints().ToGPALObject();

            if (true == stopRequested)
                return;

            SetStatus("Calling /api/basicstore/products with GET...");

            string data = restClient.WithEndpoint(@"/api/basicstore/products").Execute();
            string pretty = null;
            GPAL.Converter.WithInput(data).PrettyPrintTo(out pretty);
            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"[{pretty}]");

            if (true == stopRequested)
                return;

            SetStatus("Calling /api/basicstore/products with OPTIONS...");

            data = restClient.WithEndpoint(@"/api/basicstore/products").WithHttpMethod("OPTIONS").Execute();
            pretty = null;
            GPAL.Converter.WithInput(data).PrettyPrintTo(out pretty);
            GPAL.PublishSimpleEvent(GPALEventType.NOTICE, $"[{pretty}]");
        }

        static void OnGPALEvent(object sender, GPAL.GPALEventArgs e)
        {
            string tag = e.GPALEventType == GPALEventType.WARNING   ? "WARN"
                       : e.GPALEventType == GPALEventType.ERROR     ? "ERR "
                       : e.GPALEventType == GPALEventType.EXCEPTION ? "EXCP"
                       : e.GPALEventType == GPALEventType.FAILURE   ? "FAIL"
                       : e.GPALEventType == GPALEventType.NOTICE    ? "NOTE"
                       : e.GPALEventType == GPALEventType.DEBUG     ? "DBG "
                       : "INFO";
            string line = $"[{e.DateTimeStamp:HH:mm:ss}] [{tag}] {e.Message}";
            if (e.ExceptionRaised != null) line += $"  ({e.ExceptionRaised.Message})";
            Debug.WriteLine(line);

            // events arrive from the run's thread and the ui thread both
            lock (fullLog)
                fullLog.Add(line);

            // AppendLine marshals to the ui thread itself and keeps the last 2000 lines
            if (true == logArea.IsReady)
                logArea.AppendLine(line, 2_000);
        }

        static void OnClearLog(object s, EventArgs e)
        {
            lock (fullLog)
                fullLog.Clear();

            logArea.Text = string.Empty;
        }

        // Asks where to save, then writes every line since the last Clear Log, up to the moment of the click.
        static void OnSaveLog(object s, EventArgs e)
        {
            using (System.Windows.Forms.SaveFileDialog dialog = new System.Windows.Forms.SaveFileDialog
            {
                Title = "Save the log",
                Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
                FileName = $"{System.Diagnostics.Process.GetCurrentProcess().ProcessName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            })
            {
                if (System.Windows.Forms.DialogResult.OK == dialog.ShowDialog())
                {
                    string[] lines;

                    lock (fullLog)
                        lines = fullLog.ToArray();

                    System.IO.File.WriteAllLines(dialog.FileName, lines);

                    SetStatus($"Saved [{lines.Length}] log lines to [{dialog.FileName}]");
                }
            }
        }

        static void SetStatus(string text)
        {
            statusBar.Text = text;
        }
    }
}
