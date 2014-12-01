﻿using Advanced_Combat_Tracker;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;

namespace RainbowMage.OverlayPlugin
{
    class SpellTimerOverlay : OverlayBase<OverlayConfig>
    {
        static DataContractJsonSerializer jsonSerializer =
            new DataContractJsonSerializer(typeof(List<SerializableTimerFrameEntry>));

        IList<SerializableTimerFrameEntry> activatedTimers;

        protected override OverlayConfig Config
        {
            get { return pluginMain.Config.SpellTimerOverlay; }
        }

        public SpellTimerOverlay(PluginMain pluginMain)
            : base(pluginMain, "SpellTimerOverlay")
        {
            this.activatedTimers = new List<SerializableTimerFrameEntry>();

            ActGlobals.oFormSpellTimers.OnSpellTimerNotify += (t) =>
            {
                lock (this.activatedTimers)
                {
                    var timerFrame = activatedTimers.Where(x => x.Original == t).FirstOrDefault();
                    if (timerFrame == null)
                    {
                        timerFrame = new SerializableTimerFrameEntry(t);
                        this.activatedTimers.Add(timerFrame);
                    }
                    foreach (var spellTimer in t.SpellTimers)
                    {
                        var timer = timerFrame.SpellTimers.Where(x => x.Original == spellTimer).FirstOrDefault();
                        if (timer == null)
                        {
                            timer = new SerializableSpellTimerEntry(spellTimer);
                            timerFrame.SpellTimers.Add(timer);
                        }
                    }
                }
            };
            ActGlobals.oFormSpellTimers.OnSpellTimerRemoved += (t) =>
            {
                //activatedTimers.Remove(t);
            };
        }

        protected override void Update()
        {
            try
            {
                var updateScript = CreateUpdateString();

                if (this.Overlay != null &&
                    this.Overlay.Renderer != null &&
                    this.Overlay.Renderer.Browser != null)
                {
                    this.Overlay.Renderer.Browser.GetMainFrame().ExecuteJavaScript(updateScript, null, 0);
                }

            }
            catch (Exception ex)
            {
                pluginMain.Log("Error: {0}: Update: {1}", this.Name, ex);
            }
        }

        private void RemoveExpiredEntries()
        {
            var expiredTimerFrames = new List<SerializableTimerFrameEntry>();
            foreach (var timerFrame in activatedTimers)
            {
                var expiredSpellTimers = new List<SerializableSpellTimerEntry>();
                bool expired = true;
                foreach (var timer in timerFrame.SpellTimers)
                {
                    if (timerFrame.StartCount - timerFrame.ExpireCount > (DateTime.Now - timer.StartTime).TotalSeconds)
                    {
                        expired = false;
                        break;
                    }
                    else
                    {
                        expiredSpellTimers.Add(timer);
                    }
                }
                if (expired)
                {
                    expiredTimerFrames.Add(timerFrame);
                }
                else
                {
                    foreach (var expiredSpellTimer in expiredSpellTimers)
                    {
                        timerFrame.SpellTimers.Remove(expiredSpellTimer);
                    }
                }
            }
            foreach (var expiredTimerFrame in expiredTimerFrames)
            {
                activatedTimers.Remove(expiredTimerFrame);
            }
        }

        internal string CreateUpdateString()
        {
            lock (this.activatedTimers)
            {
                RemoveExpiredEntries();
            }

            using (var ms = new MemoryStream())
            {
                lock (this.activatedTimers)
                {
                    RemoveExpiredEntries();
                    jsonSerializer.WriteObject(ms, activatedTimers);
                }

                var result = Encoding.UTF8.GetString(ms.ToArray());

                if (!string.IsNullOrWhiteSpace(result))
                {
                    return string.Format("{0}{1}{2}", "ActXiv = { timerFrames: ", result, "};");
                }
                else
                {
                    return "";
                }
            }
        }
    }
}
