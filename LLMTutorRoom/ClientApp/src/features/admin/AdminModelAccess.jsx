import { ConfirmDialog } from "../../shared/ui/ConfirmDialog.jsx";
import { AccessDirectory } from "./modelAccess/AccessDirectory.jsx";
import { AccessEditor } from "./modelAccess/AccessEditor.jsx";
import { ActionBanner } from "./modelAccess/ActionBanner.jsx";
import { AdminModelAccessHeader } from "./modelAccess/AdminModelAccessHeader.jsx";
import { ModelAccessInitialState } from "./modelAccess/ModelAccessInitialState.jsx";
import { TeacherPicker } from "./modelAccess/TeacherPicker.jsx";
import { useAdminModelAccess } from "./modelAccess/useAdminModelAccess.js";

export function AdminModelAccess() {
  const modelAccess = useAdminModelAccess();

  return (
    <>
      <section className="admin-page admin-model-access-page">
        <AdminModelAccessHeader
          isLoading={modelAccess.isLoading}
          modelCount={modelAccess.models.length}
          teacherCount={modelAccess.teachers.length}
        />

        {modelAccess.isLoading || modelAccess.initialError ? (
          <ModelAccessInitialState
            error={modelAccess.initialError}
            isLoading={modelAccess.isLoading}
            onRetry={modelAccess.retryInitialLoad}
          />
        ) : (
          <>
            <TeacherPicker
              isMutationLocked={modelAccess.isMutationLocked}
              onSelect={modelAccess.selectTeacher}
              selectedTeacher={modelAccess.selectedTeacher}
              selectedTeacherId={modelAccess.selectedTeacherId}
              teachers={modelAccess.teachers}
            />

            <ActionBanner
              actionBannerRef={modelAccess.actionBannerRef}
              message={modelAccess.actionMessage}
            />

            <div className="admin-model-access-layout">
              <AccessDirectory
                accessError={modelAccess.accessError}
                accessList={modelAccess.accessList}
                busyModelId={modelAccess.busyModelId}
                canMutateAccess={modelAccess.canMutateAccess}
                editingModelId={modelAccess.editingModelId}
                isAccessCurrent={modelAccess.isAccessCurrent}
                isAccessLoading={modelAccess.isAccessLoading}
                onEdit={modelAccess.editAccess}
                onRemove={modelAccess.requestRemoveAccess}
                onRetry={modelAccess.retryAccessLoad}
                selectedTeacher={modelAccess.selectedTeacher}
                selectedTeacherId={modelAccess.selectedTeacherId}
              />

              <AccessEditor
                canMutateAccess={modelAccess.canMutateAccess}
                editingModelId={modelAccess.editingModelId}
                editorRef={modelAccess.editorRef}
                form={modelAccess.form}
                isAccessLoading={modelAccess.isAccessLoading}
                isAccessPayloadDisabled={modelAccess.isAccessPayloadDisabled}
                isModelSelectDisabled={modelAccess.isModelSelectDisabled}
                isMutationLocked={modelAccess.isMutationLocked}
                isSaving={modelAccess.isSaving}
                models={modelAccess.models}
                onCancel={modelAccess.cancelEdit}
                onSave={modelAccess.saveAccess}
                onSelectModel={modelAccess.selectModel}
                onUpdateForm={modelAccess.updateForm}
                selectedModel={modelAccess.selectedModel}
                teachers={modelAccess.teachers}
              />
            </div>
          </>
        )}
      </section>

      {modelAccess.pendingDeleteAccess && (
        <ConfirmDialog
          title="Удалить доступ к модели?"
          description={`Доступ «${modelAccess.pendingDeleteAccess.displayName || modelAccess.pendingDeleteAccess.modelId}» для ${modelAccess.selectedTeacher?.userName ?? "преподавателя"} будет удалён.`}
          confirmLabel="Удалить доступ"
          isBusy={modelAccess.busyModelId === modelAccess.pendingDeleteAccess.modelId}
          onCancel={modelAccess.cancelDelete}
          onConfirm={modelAccess.confirmRemoveAccess}
        />
      )}
    </>
  );
}

export default AdminModelAccess;
