"use client";

import { useCallback, useState, type FormEvent } from "react";
import { Modal } from "@/components/common/Modal";
import { Button } from "@/components/common/Button";
import { usePatientStore } from "@/stores/usePatientStore";
import { useToastStore } from "@/stores/useToastStore";
import { isApiError } from "@/lib/api/client";
import type { Patient } from "@/types/patient";

interface CreatePatientModalProps {
  isOpen: boolean;
  onClose: () => void;
  onCreated: (patient: Patient) => void;
}

export function CreatePatientModal({ isOpen, onClose, onCreated }: CreatePatientModalProps) {
  const createPatient = usePatientStore((s) => s.createPatient);
  const showError = useToastStore((s) => s.error);
  const showSuccess = useToastStore((s) => s.success);

  const [name, setName] = useState("");
  const [cancerType, setCancerType] = useState("");
  const [notes, setNotes] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const reset = () => {
    setName("");
    setCancerType("");
    setNotes("");
  };

  const handleClose = useCallback(() => {
    reset();
    onClose();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [onClose]);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;
    setIsSubmitting(true);
    try {
      const patient = await createPatient({
        name: name.trim(),
        cancerType: cancerType.trim() || undefined,
        notes: notes.trim() || undefined,
      });
      showSuccess("Patient created", patient.name);
      reset();
      onCreated(patient);
    } catch (err) {
      showError(
        "Could not create patient",
        isApiError(err) ? err.detail ?? err.message : "Unexpected error"
      );
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Modal isOpen={isOpen} onClose={handleClose} title="New patient" size="sm">
      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        <label className="flex flex-col gap-1.5">
          <span className="text-ui text-ink">Name</span>
          <input
            autoFocus
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            className="rounded-md border border-rule-strong px-3 py-2.5 text-ui text-ink outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-1"
          />
        </label>
        <label className="flex flex-col gap-1.5">
          <span className="text-ui text-ink">Cancer type</span>
          <input
            value={cancerType}
            onChange={(e) => setCancerType(e.target.value)}
            placeholder="e.g. Melanoma"
            className="rounded-md border border-rule-strong px-3 py-2.5 text-ui text-ink outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-1"
          />
        </label>
        <label className="flex flex-col gap-1.5">
          <span className="text-ui text-ink">Notes</span>
          <textarea
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={3}
            className="rounded-md border border-rule-strong px-3 py-2.5 text-ui text-ink outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-1"
          />
        </label>
        <div className="mt-2 flex justify-end gap-3">
          <Button type="button" variant="secondary" onClick={handleClose}>
            Cancel
          </Button>
          <Button type="submit" isLoading={isSubmitting} disabled={!name.trim()}>
            Create
          </Button>
        </div>
      </form>
    </Modal>
  );
}
