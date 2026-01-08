import { useState, useEffect } from "react";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "sonner";

interface EditQuestionDialogProps {
  question: { id: number; questionText: string; order: number } | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSave: () => void;
}

const EditQuestionDialog = ({
  question,
  open,
  onOpenChange,
  onSave,
}: EditQuestionDialogProps) => {
  const [questionText, setQuestionText] = useState("");
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (question) {
      setQuestionText(question.questionText);
    }
  }, [question]);

  const handleSave = async () => {
    if (!question) return;

    if (!questionText.trim()) {
      toast.error("Текст вопроса обязателен");
      return;
    }

    setLoading(true);
    try {
      const response = await fetch(`/api/Survey/questions/${question.id}`, {
        method: "PUT",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          questionText: questionText.trim(),
          order: question.order, // порядок не меняем
        }),
      });

      if (response.ok) {
        toast.success("Вопрос обновлен");
        onSave();
        onOpenChange(false);
      } else {
        let errorMessage = "Не удалось обновить вопрос";
        try {
          const error = await response.json();
          errorMessage = error.error || error.message || errorMessage;
        } catch {
          errorMessage = `Ошибка ${response.status}: ${response.statusText}`;
        }
        toast.error(errorMessage);
      }
    } catch (error) {
      console.error("Failed to update question:", error);
      toast.error("Ошибка при обновлении вопроса");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Редактировать вопрос</DialogTitle>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <div className="space-y-2">
            <Label htmlFor="questionText">Текст вопроса *</Label>
            <Textarea
              id="questionText"
              value={questionText}
              onChange={(e) => setQuestionText(e.target.value)}
              placeholder="Введите текст вопроса"
              rows={3}
            />
          </div>

        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Отмена
          </Button>
          <Button onClick={handleSave} disabled={loading}>
            {loading ? "Сохранение..." : "Сохранить"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

export default EditQuestionDialog;

