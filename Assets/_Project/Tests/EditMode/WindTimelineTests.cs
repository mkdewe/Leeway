using Leeway.World.Domain;
using NUnit.Framework;
using UnityEngine;

namespace Leeway.Tests
{
    public class WindTimelineTests
    {
        [Test]
        public void ChangeStarts_AreRandomlySpacedBetweenFiveAndFifteenMinutes()
        {
            var wind=new WindTimeline(1843);
            var state=wind.Evaluate(0);
            double previous=0;bool different=false;double lastInterval=-1;
            for(int i=0;i<40;i++)
            {
                double interval=state.NextChangeTime-previous;
                Assert.That(interval,Is.InRange(300.0,900.0));
                if(lastInterval>0&&System.Math.Abs(interval-lastInterval)>1)different=true;
                lastInterval=interval;previous=state.NextChangeTime;
                state=wind.Evaluate(previous+0.001);
            }
            Assert.IsTrue(different,"Weather periods must not become a fixed repeating timer");
        }

        [Test]
        public void DirectionAndCloudPosition_AreContinuousAcrossEveryChange()
        {
            var wind=new WindTimeline(782);
            var state=wind.Evaluate(0);
            for(int i=0;i<20;i++)
            {
                double boundary=state.NextChangeTime;
                var before=wind.Evaluate(boundary-0.001);
                var after=wind.Evaluate(boundary+0.001);
                Assert.Less(Vector2.Distance(before.Direction,after.Direction),0.0001f);
                Assert.Less(Vector2.Distance(before.CloudOffset,after.CloudOffset),0.0003f,
                    "Changing wind must not teleport the cloud layer");
                Assert.AreEqual(before.Strength,after.Strength,0.0001f);
                var completed=wind.Evaluate(boundary+31);
                Assert.Greater(Vector2.Angle(before.Direction,completed.Direction),30);
                Assert.That(completed.Strength,Is.InRange(0.449f,1.151f));
                state=completed;
            }
        }

        [Test]
        public void LateJoiningClient_EvaluatesTheSameWeatherAsTheHost()
        {
            var host=new WindTimeline(47117);
            for(int time=0;time<86400;time+=60)host.Evaluate(time);
            var client=new WindTimeline(47117);
            var a=host.Evaluate(86400);var b=client.Evaluate(86400);
            Assert.AreEqual(a.ChangeIndex,b.ChangeIndex);
            Assert.AreEqual(a.Direction,b.Direction);
            Assert.AreEqual(a.Strength,b.Strength);
            Assert.AreEqual(a.CloudOffset,b.CloudOffset);
            Assert.AreEqual(a.NextChangeTime,b.NextChangeTime);
        }

        [Test]
        public void DifferentIslandSeeds_ProduceDifferentWeather()
        {
            var a=new WindTimeline(1).Evaluate(3600);
            var b=new WindTimeline(2).Evaluate(3600);
            Assert.Greater(Vector2.Distance(a.CloudOffset,b.CloudOffset),0.1f);
            Assert.AreNotEqual(a.NextChangeTime,b.NextChangeTime);
        }
    }
}
