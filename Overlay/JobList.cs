using DV.Logic.Job;
using DV.ThingTypes;
using DV.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DvMod.HeadsUpDisplay
{
    public class JobList
    {
        public static void DrawJobList()
        {
            if (!Main.settings.drawJobList)
            {
                return;
            }
            var jobs = (SingletonBehaviour<JobsManager>.Instance.currentJobs ?? new List<Job>()).Select(_ => new JobOutput(_)).ToList();

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            foreach (var job in jobs)
            {
                GUILayout.Label(job.ID, Styles.noWrap);
                buildDummyLabels(job);
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label(" ", Styles.noWrap);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            foreach (var job in jobs)
            {
                GUILayout.Label(job.Type, Styles.noWrap);
                buildDummyLabels(job);
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label(" ", Styles.noWrap);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            foreach (var job in jobs)
            {
                GUI.contentColor = job.TimeRemainingColor;
                GUILayout.Label(job.TimeRemaining, Styles.noWrap);
                GUI.contentColor = Color.white;
                buildDummyLabels(job);
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label(" ", Styles.noWrap);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            foreach (var job in jobs)
            {
                GUILayout.Label(job.BasePayment, Styles.noWrap);
                buildDummyLabels(job);
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label(" ", Styles.noWrap);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            foreach (var job in jobs)
            {
                for (int i = 0; i < job.Tasks.Count; i++)
                {
                    GUI.contentColor = job.Tasks[i].Color;
                    GUILayout.Label(job.Tasks[i].Text, Styles.noWrap);
                }
            }
            GUI.contentColor = Color.white;
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            static void buildDummyLabels(JobOutput job)
            {
                for (int i = 0; i < job.Tasks.Count - 1; i++)
                {
                    GUILayout.Label(" ", Styles.noWrap);
                }
            }
        }
        private static FieldInfo seqTasksField = typeof(SequentialTasks).GetField("tasks", BindingFlags.Instance | BindingFlags.NonPublic);
        private static FieldInfo parTasksField = typeof(ParallelTasks).GetField("tasks", BindingFlags.Instance | BindingFlags.NonPublic);
        public static void RecursiveTaskLog(Task task, int depth, int index, List<TaskOutput> output)
        {
            var item = new TaskOutput();
            var data = task.GetTaskData();
            item.Color = data.state switch
            {
                // Tasks seem to always be in progress when picked up
                TaskState.InProgress => Color.white,
                TaskState.Done => Color.green,
                TaskState.Failed => Color.red,
                _ => Color.yellow
            };
            if (data.anyHandbrakeRequiredAndNotDone)
            {
                item.Color = Color.red;
            }
            string prefix = $"{new string(' ', depth)} {(index > 0 ? index.ToString() + ":" : "-")} ";
            switch (task)
            {
                case SequentialTasks _:
                    {
                        var tasks = (LinkedList<Task>)seqTasksField.GetValue(task);

                        if (tasks.Count == 1)
                        {
                            RecursiveTaskLog(tasks.First.Value, depth, index, output);
                        }
                        else
                        {
                            item.Text = $"{prefix}in specified order:";
                            output.Add(item);
                            var tasksEnumerator = tasks.GetEnumerator();
                            for (int i = 0; tasksEnumerator.MoveNext(); i++)
                            {
                                RecursiveTaskLog(tasksEnumerator.Current, depth + 1, i + 1, output);
                            }
                        }
                        return;
                    }

                case ParallelTasks _:
                    {
                        var tasks = (List<Task>)parTasksField.GetValue(task);
                        if (tasks.Count == 1)
                        {
                            RecursiveTaskLog(tasks[0], depth, index, output);
                        }
                        else
                        {
                            item.Text = $"{prefix}in any order:";
                            output.Add(item);
                            for (int i = 0; i < tasks.Count; i++)
                            {
                                Task? task2 = tasks[i];
                                RecursiveTaskLog(task2, depth + 1, -1, output);
                            }
                        }
                        return;
                    }

                case WarehouseTask _:
                    {
                        item.Text = $"{prefix}{(data.warehouseTaskType == WarehouseTaskType.Loading ? "Load" : "Unload")} destinationTrack: {data.destinationTrack?.ID}";
                        output.Add(item);
                        return;
                    }
                case TransportTask _:
                    {
                        item.Text = $"{prefix}Move {data.cars?.Count} car(s) from {data.startTrack?.ID} to {data.destinationTrack?.ID}";
                        output.Add(item);
                        return;
                    }
            }

        }
        public class JobOutput
        {
            public string ID;
            public string Type;
            public Color TimeRemainingColor;
            public string TimeRemaining;
            public string BasePayment;
            public List<TaskOutput> Tasks;

            public JobOutput(Job job)
            {
                ID = job.ID;
                Type = job.jobType switch
                {
                    JobType.ShuntingLoad => "Shunting Load",
                    JobType.ShuntingUnload => "Shunting Unload",
                    JobType.Transport => "Freight Haul",
                    JobType.EmptyHaul => "Logistical Haul",
                    JobType.ComplexTransport => "Transport",
                    JobType.Custom => "Custom",
                    _ => job.jobType.ToString()
                };
                if (job.State == JobState.InProgress)
                {
                    double totalMinutes = TimeSpan.FromSeconds(job.TimeLimit - job.GetTimeOnJob()).TotalMinutes;

                    TimeRemainingColor = totalMinutes > 5 ? Color.white : totalMinutes > 1 ? Color.yellow : Color.red;

                    TimeRemaining = $"{(int)totalMinutes}:{Math.Floor((totalMinutes % 1) * 60):00}";
                }
                else
                {
                    TimeRemainingColor = Color.white;
                    TimeRemaining = " ";
                }
                BasePayment = $"${job.GetBasePaymentForTheJob()}";
                Tasks = new List<TaskOutput>();
                foreach (var item in job.tasks)
                {
                    RecursiveTaskLog(item, 0, 0, Tasks);
                }
            }

        }
        public class TaskOutput
        {
            public Color Color = Color.grey;
            public string Text = string.Empty;
        }
    }
}
