﻿using System;
using System.Collections.Generic;
using System.Concurrency;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using ReactiveUI.Testing;
using Xunit;

namespace ReactiveUI.Tests
{
    public class HostTestFixture : ReactiveObject
    {
        public TestFixture _Child;
        public TestFixture Child {
            get { return _Child; }
            set { this.RaiseAndSetIfChanged(x => x.Child, value); }
        }

        public int _SomeOtherParam;
        public int SomeOtherParam {
            get { return _SomeOtherParam; }
            set { this.RaiseAndSetIfChanged(x => x.SomeOtherParam, value); }
        }

        public NonObservableTestFixture _PocoChild;
        public NonObservableTestFixture PocoChild {
            get { return _PocoChild; }
            set { this.RaiseAndSetIfChanged(x => x.PocoChild, value); }
        }
    }

    public class NonObservableTestFixture
    {
        public TestFixture Child {get; set;}
    }

    public class ReactiveNotifyPropertyChangedMixinTest : IEnableLogger
    {
        [Fact]
        public void OFPSimplePropertyTest()
        {
            (new TestScheduler()).With(sched => {
                var fixture = new TestFixture();
                var changes = fixture.ObservableForProperty(x => x.IsOnlyOneWord).CreateCollection();

                fixture.IsOnlyOneWord = "Foo";
                sched.Run();
                Assert.Equal(1, changes.Count);

                fixture.IsOnlyOneWord = "Bar";
                sched.Run();
                Assert.Equal(2, changes.Count);

                fixture.IsOnlyOneWord = "Baz";
                sched.Run();
                Assert.Equal(3, changes.Count);

                fixture.IsOnlyOneWord = "Baz";
                sched.Run();
                Assert.Equal(3, changes.Count);

                Assert.True(changes.All(x => x.Sender == fixture));
                Assert.True(changes.All(x => x.PropertyName == "IsOnlyOneWord"));
                changes.Select(x => x.Value).AssertAreEqual(new[] {"Foo", "Bar", "Baz"});
            });
        }

        [Fact]
        public void OFPSimpleChildPropertyTest()
        {
            (new TestScheduler()).With(sched => {
                var fixture = new HostTestFixture() {Child = new TestFixture()};
                var changes = fixture.ObservableForProperty(x => x.Child.IsOnlyOneWord).CreateCollection();

                fixture.Child.IsOnlyOneWord = "Foo";
                sched.Run();
                Assert.Equal(1, changes.Count);

                fixture.Child.IsOnlyOneWord = "Bar";
                sched.Run();
                Assert.Equal(2, changes.Count);

                fixture.Child.IsOnlyOneWord = "Baz";
                sched.Run();
                Assert.Equal(3, changes.Count);

                fixture.Child.IsOnlyOneWord = "Baz";
                sched.Run();
                Assert.Equal(3, changes.Count);

                Assert.True(changes.All(x => x.Sender == fixture));
                Assert.True(changes.All(x => x.PropertyName == "Child.IsOnlyOneWord"));
                changes.Select(x => x.Value).AssertAreEqual(new[] {"Foo", "Bar", "Baz"});
            });
        }

        [Fact]
        public void OFPReplacingTheHostShouldResubscribeTheObservable()
        {
            (new TestScheduler()).With(sched => {
                var fixture = new HostTestFixture() {Child = new TestFixture()};
                var changes = fixture.ObservableForProperty(x => x.Child.IsOnlyOneWord).CreateCollection();

                fixture.Child.IsOnlyOneWord = "Foo";
                sched.Run();
                Assert.Equal(1, changes.Count);

                fixture.Child.IsOnlyOneWord = "Bar";
                sched.Run();
                Assert.Equal(2, changes.Count);

                // Tricky! This is a change too, because from the perspective 
                // of the binding, we've went from "Bar" to null
                fixture.Child = new TestFixture();
                sched.Run();
                Assert.Equal(3, changes.Count);

                // Here we've set the value but it shouldn't change
                fixture.Child.IsOnlyOneWord = null;
                sched.Run();
                Assert.Equal(3, changes.Count);

                fixture.Child.IsOnlyOneWord = "Baz";
                sched.Run();
                Assert.Equal(4, changes.Count);

                fixture.Child.IsOnlyOneWord = "Baz";
                sched.Run();
                Assert.Equal(4, changes.Count);

                Assert.True(changes.All(x => x.Sender == fixture));
                Assert.True(changes.All(x => x.PropertyName == "Child.IsOnlyOneWord"));
                changes.Select(x => x.Value).AssertAreEqual(new[] {"Foo", "Bar", null, "Baz"});
            });           
        }

        [Fact]
        public void ChangingTheHostPropertyShouldFireAChildChangeNotificationOnlyIfThePreviousChildIsDifferent()
        {
            (new TestScheduler()).With(sched => {
                var fixture = new HostTestFixture() {Child = new TestFixture()};
                var changes = fixture.ObservableForProperty(x => x.Child.IsOnlyOneWord).CreateCollection();

                fixture.Child.IsOnlyOneWord = "Foo";
                sched.Run();
                Assert.Equal(1, changes.Count);

                fixture.Child.IsOnlyOneWord = "Bar";
                sched.Run();
                Assert.Equal(2, changes.Count);

                fixture.Child = new TestFixture() {IsOnlyOneWord = "Bar"};
                sched.Run();
                Assert.Equal(2, changes.Count);
            });
        }

        [Fact]
        public void MultiPropertyExpressionsShouldBeProperlyResolved()
        {
            var data = new Dictionary<Expression<Func<HostTestFixture, object>>, string[]>() {
                {x => x.SomeOtherParam, new[] {"SomeOtherParam"}},
                {x => x.Child.IsNotNullString, new[] {"Child", "IsNotNullString"}},
                {x => x.Child.Changed, new[] {"Child", "Changed"}},
                {x => x.Child.IsOnlyOneWord.Length, new[] {"Child", "IsOnlyOneWord", "Length"}},
            };

            var results = data.Keys.Select(x => new {input = x, output = RxApp.expressionToPropertyNames(x)});

            results.Run(x => {
                this.Log().InfoFormat("Attempted {0}, expected [{1}]", x.input, String.Join(",", data[x.input]));
                data[x.input].AssertAreEqual(x.output);
            });
        }
    }
}