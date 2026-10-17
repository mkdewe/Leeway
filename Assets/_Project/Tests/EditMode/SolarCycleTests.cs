using NUnit.Framework;
using UnityEngine;
using Leeway.World.Domain;
namespace Leeway.Tests {
public class SolarCycleTests {
[Test] public void FullCycleReturnsToSameSky() {
var a=SolarCycle.Evaluate(0,10,24);var b=SolarCycle.Evaluate(1440,10,24);
Assert.That(b.Hour,Is.EqualTo(a.Hour).Within(0.001f));
Assert.That(Vector3.Distance(a.SunDirection,b.SunDirection),Is.LessThan(0.0001f));}
[Test] public void NoonAndMidnightHaveOppositeLighting() {
var a=SolarCycle.Evaluate(0,12,24);var b=SolarCycle.Evaluate(0,0,24);
Assert.That(a.Daylight,Is.EqualTo(1));Assert.That(b.Daylight,Is.EqualTo(0));
Assert.That(Vector3.Distance(b.MoonDirection,a.SunDirection),Is.LessThan(0.0001f));}
[Test] public void HorizonAndMidnightAreContinuous() {
foreach(float h in new[]{0f,6f,18f,24f}) {
var a=SolarCycle.Evaluate(-0.01,h,24);var b=SolarCycle.Evaluate(0.01,h,24);
Assert.That(Vector3.Distance(a.SunDirection,b.SunDirection),Is.LessThan(0.001f));
Assert.That(Mathf.Abs(a.Daylight-b.Daylight),Is.LessThan(0.001f));}}
[Test] public void AuthoritativeClockDeterminesTimeWithoutLocalHistory() {
var a=SolarCycle.Evaluate(86765.25,10,24);var b=SolarCycle.Evaluate(88205.25,10,24);
Assert.That(b.Hour,Is.EqualTo(a.Hour).Within(0.001f));}
}}