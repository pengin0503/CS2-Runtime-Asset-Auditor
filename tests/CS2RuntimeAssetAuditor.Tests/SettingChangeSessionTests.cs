using System;
using System.Linq;
using CS2RuntimeAssetAuditor.Core.Advisor;
using NUnit.Framework;

namespace CS2RuntimeAssetAuditor.Tests
{
    [TestFixture]
    public sealed class SettingChangeSessionTests
    {
        [Test]
        public void Original_value_is_captured_immediately_before_apply_and_is_safe_to_restore()
        {
            var session = new SettingChangeSession();
            session.RecordPending("shadow", "High", "Medium", DateTime.UtcNow);
            session.RecordApplied("shadow", "High", "Medium", DateTime.UtcNow);
            var change = session.Changes.Single();
            Assert.That(change.OriginalValue, Is.EqualTo("High"));
            Assert.That(change.AppliedValue, Is.EqualTo("Medium"));
            Assert.That(session.EvaluateUndo("shadow", "Medium"), Is.EqualTo(UndoDecision.SafeRestore));
        }

        [Test]
        public void External_modification_blocks_silent_restore_and_records_the_conflict()
        {
            var session = new SettingChangeSession();
            session.RecordApplied("shadow", "High", "Medium", DateTime.UtcNow);
            Assert.That(session.EvaluateUndo("shadow", "Low"), Is.EqualTo(UndoDecision.Conflict));
            Assert.That(session.Changes.Single().Status, Is.EqualTo(SettingChangeStatus.ExternallyModified));
            Assert.That(session.Changes.Single().CurrentObservedValue, Is.EqualTo("Low"));
        }

        [Test]
        public void Undo_session_iterates_newest_first_and_skips_conflicts()
        {
            var session = new SettingChangeSession();
            session.RecordApplied("shadow", "High", "Medium", DateTime.UtcNow.AddSeconds(-2));
            session.RecordApplied("texture", "High", "Medium", DateTime.UtcNow);
            var planned = session.PlanSessionUndo();
            Assert.That(planned.Select(x => x.SettingId), Is.EqualTo(new[] { "texture", "shadow" }));
            Assert.That(session.EvaluateUndo("texture", "Low"), Is.EqualTo(UndoDecision.Conflict));
            Assert.That(session.PlanSessionUndo().Select(x => x.SettingId), Is.EqualTo(new[] { "shadow" }));
        }

        [Test]
        public void Failed_apply_is_recorded_but_never_undoable()
        {
            var session = new SettingChangeSession();
            session.RecordPending("shadow", "High", "Medium", DateTime.UtcNow);
            session.MarkApplyFailed("shadow", "High");
            Assert.That(session.PlanSessionUndo(), Is.Empty);
            Assert.That(session.Changes.Single().Status, Is.EqualTo(SettingChangeStatus.ApplyFailed));
        }

        [Test]
        public void Already_restored_setting_does_not_write_again()
        {
            var session = new SettingChangeSession();
            session.RecordApplied("shadow", "High", "Medium", DateTime.UtcNow);
            Assert.That(session.EvaluateUndo("shadow", "High"), Is.EqualTo(UndoDecision.AlreadyRestored));
            Assert.That(session.PlanSessionUndo(), Is.Empty);
        }
    }
}
