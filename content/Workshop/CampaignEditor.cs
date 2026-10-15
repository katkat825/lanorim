using System;
using System.Collections.Generic;
using Content.Schema;

namespace Content.Workshop
{
    // A CAMPAIGN OPEN ON ITS PAGE (cc_task_m Parts 4-6): the draft its tabs edit, the one undo stack they share, whether
    // it's saved, the autosave, and what the campaign check says about it. Every change goes through Edit, which keeps
    // the draft whole before and after so a step undoes and redoes as one; the tabs' own operations (the chapters,
    // the characters) are Edits with names (CampaignEditor.Chapters.cs, CampaignEditor.People.cs)
    public sealed partial class CampaignEditor
    {
        string _saved;

        public CampaignEditor(string folder, CampaignDraft draft)
        {
            Folder = folder ?? throw new ArgumentNullException(nameof(folder));
            Draft = draft ?? throw new ArgumentNullException(nameof(draft));
            _saved = Draft.Snapshot();
        }

        // the campaign as it is on disk, and what reading it said
        public static CampaignEditor Open(string folder)
        {
            var read = new List<ContentProblem>();
            var editor = new CampaignEditor(folder, CampaignFiles.Open(folder, read));
            editor.Refresh();
            return editor;
        }

        public string Folder { get; }

        public string Id => Draft.Id;

        public CampaignDraft Draft { get; private set; }

        public UndoStack Undo { get; } = new();

        // anything changed since the real files were last written (or read)
        public bool Unsaved => Draft.Snapshot() != _saved;

        // A CHANGE, AS ONE UNDO STEP: the draft kept whole before and after, and put back whole. false when it changed
        // nothing (a form field set to what it already said), and then there's no step. `typing`: the same field typed
        // into again folds into its last step (UndoStack.Push's merge)
        public bool Edit(string what, Action<CampaignDraft> change, bool typing = false)
        {
            string before = Draft.Snapshot();

            change(Draft);

            string after = Draft.Snapshot();

            if (after == before) return false;

            Undo.Push(what, () => Put(before), () => Put(after), typing);
            return true;
        }

        void Put(string snapshot) => Draft = CampaignDraft.FromSnapshot(snapshot) ?? Draft;

        // --- saving -------------------------------------------------------------------------------------------------

        // SAVE: the real files, problems or not; the autosave thrown away; the problems read again. null when it saved,
        // or the disk's reason it didn't
        public string Save()
        {
            string refused = CampaignFiles.Save(Draft, Folder);

            if (refused != null) return refused;

            _saved = Draft.Snapshot();
            Autosave.Forget(Folder);
            Refresh();
            return null;
        }

        // the autosave, when there's anything unsaved to keep
        public string KeepDraft() => Unsaved ? Autosave.Write(Folder, Draft) : null;

        // "You have unsaved changes from last time": the draft is newer than the files
        public bool HasDraftFromLastTime => Autosave.Newer(Folder);

        // Keep them: the draft becomes the page, unsaved (Save writes it); one undo step takes it back to the files
        public void TakeDraft()
        {
            CampaignDraft kept = Autosave.Read(Folder);

            if (kept != null) Edit("draft", d => Draft = kept);
        }

        // Throw them away
        public void DropDraft() => Autosave.Forget(Folder);

        // --- the campaign check -------------------------------------------------------------------------------------

        public IReadOnlyList<ContentProblem> Problems { get; private set; } = Array.Empty<ContentProblem>();

        // read again from the files (on every save): Package.Read and the story's check, in plain words (WorkshopProblems)
        public void Refresh() => Problems = WorkshopProblems.Of(Folder);

        public override string ToString() => $"{Draft}{(Unsaved ? ", unsaved" : "")}";
    }
}
