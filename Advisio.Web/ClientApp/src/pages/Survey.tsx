import { useState, useEffect } from "react";
import { Link, useNavigate } from "react-router-dom";
import Layout from "@/components/Layout";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { Label } from "@/components/ui/label";
import { ClipboardList, CheckCircle2, Sparkles, Edit, Trash2, Plus } from "lucide-react";
import { useAuth } from "@/contexts/AuthContext";
import { toast } from "sonner";
import { isAdmin } from "@/lib/utils";
import EditQuestionDialog from "@/components/EditQuestionDialog";
import EditAnswerDialog from "@/components/EditAnswerDialog";
import CreateAnswerDialog from "@/components/CreateAnswerDialog";
import CreateQuestionDialog from "@/components/CreateQuestionDialog";

interface SurveyAnswer {
  id: number;
  answerText: string;
  weight: number;
}

interface SurveyQuestion {
  id: number;
  questionText: string;
  order: number;
  answers: SurveyAnswer[];
}

interface Survey {
  id: number;
  title: string;
  description: string;
  questions: SurveyQuestion[];
}

const Survey = () => {
  const navigate = useNavigate();
  const { user, isAuthenticated, isLoading } = useAuth();
  const [survey, setSurvey] = useState<Survey | null>(null);
  const [answers, setAnswers] = useState<Record<number, number>>({});
  const [submitted, setSubmitted] = useState(false);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [editQuestionDialogOpen, setEditQuestionDialogOpen] = useState(false);
  const [editAnswerDialogOpen, setEditAnswerDialogOpen] = useState(false);
  const [createAnswerDialogOpen, setCreateAnswerDialogOpen] = useState(false);
  const [createQuestionDialogOpen, setCreateQuestionDialogOpen] = useState(false);
  const [selectedQuestion, setSelectedQuestion] = useState<{ id: number; questionText: string; order: number } | null>(null);
  const [selectedAnswer, setSelectedAnswer] = useState<{ id: number; answerText: string; weight: number } | null>(null);
  const [selectedQuestionForAnswer, setSelectedQuestionForAnswer] = useState<number | null>(null);

  useEffect(() => {
    if (isAuthenticated) {
      loadSurvey();
    }
  }, [isAuthenticated]);

  const loadSurvey = async () => {
    setLoading(true);
    try {
      const response = await fetch("/api/Survey/active");
      if (response.ok) {
        const data = await response.json();
        if (data && data.questions && data.questions.length > 0) {
          setSurvey({
            id: data.id,
            title: data.title || "Опрос о предпочтениях отдыха",
            description: data.description || "Ответьте на несколько вопросов, чтобы мы могли подобрать для вас идеальные места",
            questions: data.questions.map((q: any) => ({
              id: q.id,
              questionText: q.questionText,
              order: q.order,
              answers: q.answers || [],
            })),
          });
        } else {
          setSurvey(null);
        }
      } else {
        toast.error("Не удалось загрузить опрос");
      }
    } catch (error) {
      console.error("Failed to load survey:", error);
      toast.error("Ошибка при загрузке опроса");
    } finally {
      setLoading(false);
    }
  };

  const handleAnswer = (questionId: number, optionId: number) => {
    setAnswers((prev) => ({
      ...prev,
      [questionId]: optionId,
    }));
  };

  const handleSubmit = async () => {
    if (!user || !survey) return;

    setSubmitting(true);
    try {
      const response = await fetch(`/api/Survey/submit?userId=${user.id}`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          surveyId: survey.id,
          responses: Object.entries(answers).map(([questionId, answerId]) => ({
            questionId: parseInt(questionId),
            answerId: answerId,
          })),
        }),
      });

      if (response.ok) {
        setSubmitted(true);
        toast.success("Опрос успешно отправлен!");
      } else {
        const data = await response.json();
        toast.error(data.error || "Ошибка при отправке опроса");
      }
    } catch (error) {
      console.error("Failed to submit survey:", error);
      toast.error("Ошибка при отправке опроса");
    } finally {
      setSubmitting(false);
    }
  };

  const answeredCount = Object.keys(answers).length;
  const totalQuestions = survey?.questions.length || 0;
  const canSubmit = answeredCount === totalQuestions && totalQuestions > 0;

  const handleDeleteQuestion = async (questionId: number) => {
    if (!user || !isAdmin(user.role)) return;
    
    if (!confirm("Вы уверены, что хотите удалить этот вопрос?")) {
      return;
    }

    try {
      const response = await fetch(`/api/Survey/questions/${questionId}`, {
        method: "DELETE",
      });

      if (response.ok) {
        toast.success("Вопрос удален");
        loadSurvey();
      } else {
        toast.error("Не удалось удалить вопрос");
      }
    } catch (error) {
      console.error("Failed to delete question:", error);
      toast.error("Ошибка при удалении вопроса");
    }
  };

  const handleDeleteAnswer = async (answerId: number) => {
    if (!user || !isAdmin(user.role)) return;
    
    if (!confirm("Вы уверены, что хотите удалить этот ответ?")) {
      return;
    }

    try {
      const response = await fetch(`/api/Survey/answers/${answerId}`, {
        method: "DELETE",
      });

      if (response.ok) {
        toast.success("Ответ удален");
        loadSurvey();
      } else {
        toast.error("Не удалось удалить ответ");
      }
    } catch (error) {
      console.error("Failed to delete answer:", error);
      toast.error("Ошибка при удалении ответа");
    }
  };

  const handleEditQuestion = (questionId: number) => {
    if (!user || !isAdmin(user.role) || !survey) return;
    const question = survey.questions.find((q) => q.id === questionId);
    if (question) {
      setSelectedQuestion({
        id: question.id,
        questionText: question.questionText,
        order: question.order,
      });
      setEditQuestionDialogOpen(true);
    }
  };

  const handleEditAnswer = (answerId: number) => {
    if (!user || !isAdmin(user.role) || !survey) return;
    for (const question of survey.questions) {
      const answer = question.answers.find((a) => a.id === answerId);
      if (answer) {
        setSelectedAnswer({
          id: answer.id,
          answerText: answer.answerText,
          weight: answer.weight,
        });
        setEditAnswerDialogOpen(true);
        break;
      }
    }
  };

  const handleAddQuestion = () => {
    if (!user || !isAdmin(user.role) || !survey) return;
    setCreateQuestionDialogOpen(true);
  };

  const handleAddAnswer = (questionId: number) => {
    if (!user || !isAdmin(user.role)) return;
    setSelectedQuestionForAnswer(questionId);
    setCreateAnswerDialogOpen(true);
  };

  const handleQuestionUpdated = () => {
    loadSurvey();
  };

  const handleAnswerUpdated = () => {
    loadSurvey();
  };

  const handleAnswerCreated = () => {
    loadSurvey();
  };

  if (isLoading) {
    return (
      <Layout>
        <div className="container py-16 text-center">
          <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-muted flex items-center justify-center">
            <ClipboardList className="h-8 w-8 text-muted-foreground animate-pulse" />
          </div>
          <p className="text-muted-foreground">Загрузка данных...</p>
        </div>
      </Layout>
    );
  }

  if (!isAuthenticated) {
    return (
      <Layout>
        <div className="container py-16">
          <div className="max-w-md mx-auto text-center">
            <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-warning/20 flex items-center justify-center">
              <ClipboardList className="h-8 w-8 text-warning" />
            </div>
            <h1 className="text-2xl font-display font-bold mb-4">
              Требуется авторизация
            </h1>
            <p className="text-muted-foreground mb-6">
              Необходимо войти в систему для прохождения опроса
            </p>
            <Link to="/login">
              <Button size="lg">Войти в систему</Button>
            </Link>
          </div>
        </div>
      </Layout>
    );
  }

  if (loading) {
    return (
      <Layout isAuthenticated={isAuthenticated} username={user?.username}>
        <div className="container py-16 text-center">
          <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-muted flex items-center justify-center animate-pulse">
            <ClipboardList className="h-8 w-8 text-muted-foreground" />
          </div>
          <p className="text-muted-foreground">Загрузка опроса...</p>
        </div>
      </Layout>
    );
  }

  if (submitted) {
    return (
      <Layout isAuthenticated={isAuthenticated} username={user?.username}>
        <div className="container py-16">
          <Card variant="elevated" className="max-w-lg mx-auto text-center animate-scale-in">
            <CardContent className="py-12">
              <div className="w-20 h-20 mx-auto mb-6 rounded-full bg-success/20 flex items-center justify-center">
                <CheckCircle2 className="h-10 w-10 text-success" />
              </div>
              <h2 className="text-2xl font-display font-bold mb-4">
                Спасибо за ответы!
              </h2>
              <p className="text-muted-foreground mb-6">
                Ваши ответы помогут нам подобрать для вас идеальные места для отдыха
              </p>
              <Link to="/recommendations">
                <Button size="lg" className="gap-2">
                  <Sparkles className="h-4 w-4" />
                  Перейти к рекомендациям
                </Button>
              </Link>
            </CardContent>
          </Card>
        </div>
      </Layout>
    );
  }

  if (!survey || survey.questions.length === 0) {
    return (
      <Layout isAuthenticated={isAuthenticated} username={user?.username}>
        <div className="container py-16 text-center">
          <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-muted flex items-center justify-center">
            <ClipboardList className="h-8 w-8 text-muted-foreground" />
          </div>
          <h1 className="text-2xl font-display font-bold mb-4">
            Опрос не найден
          </h1>
          <p className="text-muted-foreground">
            В данный момент нет доступных опросов
          </p>
        </div>
      </Layout>
    );
  }

  return (
    <Layout isAuthenticated={isAuthenticated} username={user?.username}>
      <div className="container py-8 md:py-12">
        <div className="max-w-2xl mx-auto">
          {/* Header */}
          <div className="text-center mb-8">
            <h1 className="text-3xl md:text-4xl font-display font-bold text-foreground mb-2">
              {survey.title}
            </h1>
            <p className="text-muted-foreground">{survey.description}</p>
          </div>

          {/* Progress */}
          <div className="mb-8">
            <div className="flex items-center justify-between text-sm mb-2">
              <span className="text-muted-foreground">Прогресс</span>
              <span className="font-medium">
                {answeredCount} из {totalQuestions}
              </span>
            </div>
            <div className="h-2 bg-muted rounded-full overflow-hidden">
              <div
                className="h-full bg-primary transition-all duration-300"
                style={{
                  width: `${(answeredCount / totalQuestions) * 100}%`,
                }}
              />
            </div>
          </div>

          {/* Questions */}
          <Card variant="elevated" className="animate-fade-in">
            <CardHeader>
              <CardTitle className="text-xl">Вопросы</CardTitle>
              <CardDescription>Выберите один вариант ответа</CardDescription>
            </CardHeader>
            <CardContent className="space-y-8">
              {survey.questions.map((question, qIndex) => (
                <div
                  key={question.id}
                  className="animate-slide-up opacity-0 border border-border rounded-lg p-4"
                  style={{
                    animationDelay: `${qIndex * 0.1}s`,
                    animationFillMode: "forwards",
                  }}
                >
                  <div className="flex items-start justify-between mb-4">
                    <h3 className="text-lg font-semibold flex-1">
                      {qIndex + 1}. {question.questionText}
                    </h3>
                    {isAdmin(user?.role) && (
                      <div className="flex items-center gap-2 ml-4">
                        <Button
                          variant="outline"
                          size="icon"
                          onClick={() => handleEditQuestion(question.id)}
                          className="h-8 w-8"
                        >
                          <Edit className="h-4 w-4" />
                        </Button>
                        <Button
                          variant="destructive"
                          size="icon"
                          onClick={() => handleDeleteQuestion(question.id)}
                          className="h-8 w-8"
                        >
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </div>
                    )}
                  </div>
                  <RadioGroup
                    value={answers[question.id]?.toString()}
                    onValueChange={(value) =>
                      handleAnswer(question.id, parseInt(value))
                    }
                    className="space-y-3"
                  >
                    {question.answers.map((answer) => (
                      <div
                        key={answer.id}
                        className="flex items-center space-x-3 p-4 rounded-lg border border-border hover:border-primary/50 hover:bg-background-light transition-all cursor-pointer group"
                      >
                        <RadioGroupItem
                          value={answer.id.toString()}
                          id={`answer-${answer.id}`}
                        />
                        <Label
                          htmlFor={`answer-${answer.id}`}
                          className="flex-1 cursor-pointer font-normal"
                        >
                          {answer.answerText}
                        </Label>
                        {isAdmin(user?.role) && (
                          <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={(e) => {
                                e.stopPropagation();
                                handleEditAnswer(answer.id);
                              }}
                              className="h-7 w-7"
                            >
                              <Edit className="h-3.5 w-3.5" />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon"
                              onClick={(e) => {
                                e.stopPropagation();
                                handleDeleteAnswer(answer.id);
                              }}
                              className="h-7 w-7 text-destructive hover:text-destructive"
                            >
                              <Trash2 className="h-3.5 w-3.5" />
                            </Button>
                          </div>
                        )}
                      </div>
                    ))}
                    {isAdmin(user?.role) && (
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => handleAddAnswer(question.id)}
                        className="w-full gap-2 mt-2"
                      >
                        <Plus className="h-4 w-4" />
                        Добавить ответ
                      </Button>
                    )}
                  </RadioGroup>
                </div>
              ))}
              {isAdmin(user?.role) && (
                <Button
                  variant="outline"
                  size="lg"
                  onClick={handleAddQuestion}
                  className="w-full gap-2 mt-4"
                >
                  <Plus className="h-4 w-4" />
                  Добавить вопрос
                </Button>
              )}

              <Button
                size="lg"
                className="w-full mt-6"
                disabled={!canSubmit || submitting}
                onClick={handleSubmit}
              >
                {submitting ? "Отправка..." : "Отправить ответы"}
              </Button>
            </CardContent>
          </Card>
        </div>

        {/* Edit Dialogs */}
        <EditQuestionDialog
          question={selectedQuestion}
          open={editQuestionDialogOpen}
          onOpenChange={setEditQuestionDialogOpen}
          onSave={handleQuestionUpdated}
        />
        <EditAnswerDialog
          answer={selectedAnswer}
          open={editAnswerDialogOpen}
          onOpenChange={setEditAnswerDialogOpen}
          onSave={handleAnswerUpdated}
        />
        <CreateAnswerDialog
          questionId={selectedQuestionForAnswer || 0}
          open={createAnswerDialogOpen}
          onOpenChange={setCreateAnswerDialogOpen}
          onSave={handleAnswerCreated}
        />
        <CreateQuestionDialog
          open={createQuestionDialogOpen}
          onOpenChange={setCreateQuestionDialogOpen}
          onSave={handleQuestionUpdated}
          defaultOrder={survey ? survey.questions.length + 1 : 1}
        />
      </div>
    </Layout>
  );
};

export default Survey;
