﻿using Metrics.MetricData;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Metrics.Core
{
    public sealed class TaggedMetricsRegistry : MetricsRegistry
    {
        private class MetricMetaCatalog<TMetric, TValue, TMetricValue>
            where TValue : MetricValueSource<TMetricValue>
        {
            public class MetricMeta
            {
                public MetricMeta(TMetric metric, TValue valueUnit)
                {
                    this.Metric = metric;
                    this.Value = valueUnit;
                }

                public string Name { get { return this.Value.Name; } }
                public TMetric Metric { get; private set; }
                public TValue Value { get; private set; }
            }

            private readonly ConcurrentDictionary<string, MetricMeta> metrics =
                new ConcurrentDictionary<string, MetricMeta>();

            public IEnumerable<TValue> All
            {
                get
                {
                    return this.metrics.Values.OrderBy(m => m.Name).Select(v => v.Value);
                }
            }

            public TMetric GetOrAdd(string name, Func<Tuple<TMetric, TValue>> metricProvider)
            {
                return this.metrics.GetOrAdd(name, n =>
                {
                    var result = metricProvider();
                    return new MetricMeta(result.Item1, result.Item2);
                }).Metric;
            }

            public void Clear()
            {
                var values = this.metrics.Values;
                this.metrics.Clear();
                foreach (var value in values)
                {
                    using (value.Metric as IDisposable) { }
                }
            }

            public void Reset()
            {
                foreach (var metric in this.metrics.Values)
                {
                    var resetable = metric.Metric as ResetableMetric;
                    if (resetable != null)
                    {
                        resetable.Reset();
                    }
                }
            }
        }

        private readonly MetricMetaCatalog<MetricValueProvider<double>, GaugeValueSource, double> gauges = new MetricMetaCatalog<MetricValueProvider<double>, GaugeValueSource, double>();
        private readonly MetricMetaCatalog<Counter, CounterValueSource, CounterValue> counters = new MetricMetaCatalog<Counter, CounterValueSource, CounterValue>();
        private readonly MetricMetaCatalog<Meter, MeterValueSource, MeterValue> meters = new MetricMetaCatalog<Meter, MeterValueSource, MeterValue>();
        private readonly MetricMetaCatalog<Histogram, HistogramValueSource, HistogramValue> histograms =
            new MetricMetaCatalog<Histogram, HistogramValueSource, HistogramValue>();
        private readonly MetricMetaCatalog<Timer, TimerValueSource, TimerValue> timers = new MetricMetaCatalog<Timer, TimerValueSource, TimerValue>();

        public TaggedMetricsRegistry()
        {
            this.DataProvider = new DefaultRegistryDataProvider(() => this.gauges.All, () => this.counters.All, () => this.meters.All, () => this.histograms.All, () => this.timers.All);
        }

        public RegistryDataProvider DataProvider { get; private set; }

        public void Gauge(string name, Func<MetricValueProvider<double>> valueProvider, Unit unit, MetricTags tags)
        {
            this.gauges.GetOrAdd(TagName(name, tags), () =>
            {
                MetricValueProvider<double> gauge = valueProvider();
                return Tuple.Create(gauge, new GaugeValueSource(name, gauge, unit, tags));
            });
        }

        public Counter Counter<T>(string name, Func<T> builder, Unit unit, MetricTags tags)
            where T : CounterImplementation
        {
            return this.counters.GetOrAdd(TagName(name, tags), () =>
            {
                T counter = builder();
                return Tuple.Create((Counter)counter, new CounterValueSource(name, counter, unit, tags));
            });
        }

        public Meter Meter<T>(string name, Func<T> builder, Unit unit, TimeUnit rateUnit, MetricTags tags)
            where T : MeterImplementation
        {
            return this.meters.GetOrAdd(TagName(name, tags), () =>
            {
                T meter = builder();
                return Tuple.Create((Meter)meter, new MeterValueSource(name, meter, unit, rateUnit, tags));
            });
        }

        public Histogram Histogram<T>(string name, Func<T> builder, Unit unit, MetricTags tags)
            where T : HistogramImplementation
        {
            return this.histograms.GetOrAdd(TagName(name, tags), () =>
            {
                T histogram = builder();
                return Tuple.Create((Histogram)histogram, new HistogramValueSource(name, histogram, unit, tags));
            });
        }

        public Timer Timer<T>(string name, Func<T> builder, Unit unit, TimeUnit rateUnit, TimeUnit durationUnit, MetricTags tags)
            where T : TimerImplementation
        {
            return this.timers.GetOrAdd(TagName(name, tags), () =>
            {
                T timer = builder();
                return Tuple.Create((Timer)timer, new TimerValueSource(name, timer, unit, rateUnit, durationUnit, tags));
            });
        }

        public bool Merge(MetricsRegistry other, bool reset)
        {
            // All of this is pretty much just copied and pasted with
            // minor changes for the tags... along with most of the rest of the file
            var tmrOther = other as TaggedMetricsRegistry;
            if (tmrOther == null)
            {
                return false;
            }

            MergeCounters(tmrOther, reset);
            MergeTimers(tmrOther, reset);
            MergeMeters(tmrOther, reset);
            MergeHistograms(tmrOther, reset);
            MergeGauges(tmrOther, reset);

            return true;
        }

        private string TagName(string name, MetricTags? tags)
        {
            if (!tags.HasValue)
            {
                return name;
            }
            return name + string.Join(".", tags.Value.Tags);
        }

        private void MergeCounters(TaggedMetricsRegistry tmrOther, bool reset)
        {
            foreach (var otherCounter in tmrOther.counters.All)
            {
                var counter = otherCounter;
                var local = (CounterImplementation)counters.GetOrAdd(
                    TagName(counter.Name, counter.Tags),
                    () =>
                        Tuple.Create((Counter)counter.ValueProvider,
                            new CounterValueSource(counter.Name, counter.ValueProvider, counter.Unit, counter.Tags)));
                if (!ReferenceEquals(local, counter.ValueProvider))
                {
                    // the item was already there - merge
                    local.Merge(counter.ValueProvider);
                }
            }
            if (reset)
            {
                tmrOther.counters.Clear();
            }
        }

        private void MergeTimers(TaggedMetricsRegistry tmrOther, bool reset)
        {
            foreach (var otherTimer in tmrOther.timers.All)
            {
                var timer = otherTimer;
                // we know that we create a TimerValueSource, which in turn, creates a ScaledValueProvider against the real timer
                var timerProvider = (ScaledValueProvider<TimerValue>) timer.ValueProvider;
                var local = (TimerImplementation)timers.GetOrAdd(
                    TagName(timer.Name, timer.Tags),
                    () =>
                        Tuple.Create((Timer)timerProvider.ValueProvider,
                            new TimerValueSource(timer.Name, timerProvider.ValueProvider, timer.Unit, timer.RateUnit, timer.DurationUnit, timer.Tags)));
                if (!ReferenceEquals(local, timerProvider.ValueProvider))
                {
                    // the item was already there - merge
                    local.Merge(timerProvider.ValueProvider);
                }
            }
            if (reset)
            {
                tmrOther.timers.Clear();
            }
        }

        private void MergeMeters(TaggedMetricsRegistry tmrOther, bool reset)
        {
            foreach (var otherMeter in tmrOther.meters.All)
            {
                var meter = otherMeter;
                // we know that we create a MeterValueSource, which in turn, creates a ScaledValueProvider against the real meter
                var meterProvider = (ScaledValueProvider<MeterValue>)meter.ValueProvider;
                var local = (MeterImplementation)meters.GetOrAdd(
                    TagName(meter.Name, meter.Tags),
                    () =>
                        Tuple.Create((Meter)meterProvider.ValueProvider,
                            new MeterValueSource(meter.Name, meterProvider.ValueProvider, meter.Unit, meter.RateUnit, meter.Tags)));
                if (!ReferenceEquals(local, meterProvider.ValueProvider))
                {
                    // the item was already there - merge
                    local.Merge(meterProvider.ValueProvider);
                }
            }
            if (reset)
            {
                tmrOther.meters.Clear();
            }
        }

        private void MergeHistograms(TaggedMetricsRegistry tmrOther, bool reset)
        {
            foreach (var otherHist in tmrOther.histograms.All)
            {
                var histogram = otherHist;
                var local = (HistogramImplementation)histograms.GetOrAdd(
                    TagName(histogram.Name, histogram.Tags),
                    () =>
                        Tuple.Create((Histogram)histogram.ValueProvider,
                            new HistogramValueSource(histogram.Name, histogram.ValueProvider, histogram.Unit, histogram.Tags)));
                if (!ReferenceEquals(local, histogram.ValueProvider))
                {
                    // the item was already there - merge
                    local.Merge(histogram.ValueProvider);
                }
            }
            if (reset)
            {
                tmrOther.histograms.Clear();
            }
        }

        private void MergeGauges(TaggedMetricsRegistry tmrOther, bool reset)
        {
            foreach (var otherGauge in tmrOther.gauges.All)
            {
                var gauge = otherGauge;
                var local = (GaugeImplementation)gauges.GetOrAdd(
                    TagName(gauge.Name, gauge.Tags),
                    () =>
                        Tuple.Create(gauge.ValueProvider,
                            new GaugeValueSource(gauge.Name, gauge.ValueProvider, gauge.Unit, gauge.Tags)));
                if (!ReferenceEquals(local, gauge.ValueProvider))
                {
                    // the item was already there - merge
                    local.Merge(gauge.ValueProvider);
                }
            }
            if (reset)
            {
                tmrOther.gauges.Clear();
            }
        }

        public void ClearAllMetrics()
        {
            this.gauges.Clear();
            this.counters.Clear();
            this.meters.Clear();
            this.histograms.Clear();
            this.timers.Clear();
        }

        public void ResetMetricsValues()
        {
            this.gauges.Reset();
            this.counters.Reset();
            this.meters.Reset();
            this.histograms.Reset();
            this.timers.Reset();
        }
    }
}
