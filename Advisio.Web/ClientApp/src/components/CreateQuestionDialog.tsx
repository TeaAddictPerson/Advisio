import { useState } from "react";
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

interface CreateQuestionDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSave: () => void;
  defaultOrder?: number;
}

const CreateQuestionDialog = ({
  open,
  onOpenChange,
  onSave,
  defaultOrder = 1,
}: CreateQuestionDialogProps) => {
  const [questionText, setQuestionText] = useState("");
  const [loading, setLoading] = useState(false);

  const handleSave = async () => {
    if (!questionText.trim()) {
      toast.error("Текст вопроса обязателен");
      return;
    }

    setLoading(true);
    try {
      const response = await fetch("/api/Survey/questions", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          questionText: questionText.trim(),
          order: defaultOrder, // новый вопрос идёт в конец
        }),
      });

      if (response.ok) {
        toast.success("Вопрос создан");
        setQuestionText("");
        onSave();
        onOpenChange(false);
      } else {
        let errorMessage = "Не удалось создать вопрос";
        try {
          const error = await response.json();
          errorMessage = error.error || error.message || errorMessage;
        } catch {
          errorMessage = `Ошибка ${response.status}: ${response.statusText}`;
        }
        toast.error(errorMessage);
      }
    } catch (error) {
      console.error("Failed to create question:", error);
      toast.error("Ошибка при создании вопроса");
    } finally {
      setLoading(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Добавить вопрос</DialogTitle>
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
            {loading ? "Создание..." : "Создать"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
};

export default CreateQuestionDialog;

