import { DndContext, PointerSensor, useDroppable, useSensor, useSensors, type DragEndEvent } from "@dnd-kit/core";
import { CSS } from "@dnd-kit/utilities";
import { useSortable } from "@dnd-kit/sortable";
import type { ProjectTask, TaskStatus } from "./types";

const columns: Array<{ status: TaskStatus; label: string }> = [{ status: "Todo", label: "To do" }, { status: "Doing", label: "In progress" }, { status: "Done", label: "Done" }];
const isOverdue = (task: ProjectTask) => Boolean(task.dueDate && new Date(task.dueDate).getTime() <= Date.now() && task.status !== "Done");
function Card({ task, canMove, onOpen }: { task: ProjectTask; canMove: boolean; onOpen: () => void }) {
  const overdue = isOverdue(task);
  const sortable = useSortable({ id: task.id, data: { status: task.status }, disabled: !canMove || overdue });
  return <article ref={sortable.setNodeRef} style={{ transform: CSS.Translate.toString(sortable.transform), transition: sortable.transition }} {...sortable.attributes} {...sortable.listeners} className={`kanban-card ${sortable.isDragging ? "dragging" : ""}`} onClick={onOpen}>
    <div><span className={`priority-badge ${task.priority.toLowerCase()}`}>{task.priority}</span><span className="comment-count">{task.comments.length} comments</span></div><h3>{task.title}</h3>{task.description && <p>{task.description}</p>}<footer><div className="avatar-stack">{task.assignees.slice(0, 3).map((a) => <span key={a.userId} title={a.displayName}>{a.displayName.slice(0, 2).toUpperCase()}</span>)}</div>{task.dueDate && <time>{new Date(task.dueDate).toLocaleDateString()}</time>}</footer>
  </article>;
}
function Column({ status, label, tasks, canMove, onOpen }: { status: TaskStatus; label: string; tasks: ProjectTask[]; canMove: boolean; onOpen: (task: ProjectTask) => void }) {
  const drop = useDroppable({ id: `column:${status}`, data: { status } });
  return <section ref={drop.setNodeRef} className={`kanban-column ${drop.isOver ? "over" : ""}`}><header><span className={`status-dot ${status.toLowerCase()}`} /><h2>{label}</h2><b>{tasks.length}</b></header><div className="kanban-list">{tasks.map((task) => <Card key={task.id} task={task} canMove={canMove} onOpen={() => onOpen(task)} />)}{!tasks.length && <div className="kanban-empty">{canMove ? "Drop tasks here" : "No tasks"}</div>}</div></section>;
}
export function KanbanBoard({ tasks, canMove, onMove, onOpen }: { tasks: ProjectTask[]; canMove: boolean; onMove: (taskId: string, status: TaskStatus, previousTaskId?: string, nextTaskId?: string) => void; onOpen: (task: ProjectTask) => void }) {
  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }));
  const dragEnd = ({ active, over }: DragEndEvent) => {
    if (!canMove || !over) return; const task = tasks.find((x) => x.id === active.id); if (!task || isOverdue(task)) return;
    const target = tasks.find((x) => x.id === over.id); const status = (target?.status ?? over.data.current?.status) as TaskStatus | undefined; if (!status) return;
    const ordered = tasks.filter((x) => x.status === status && x.id !== task.id).sort((a, b) => a.position - b.position);
    const index = target ? Math.max(0, ordered.findIndex((x) => x.id === target.id)) : ordered.length;
    onMove(task.id, status, ordered[index - 1]?.id, ordered[index]?.id);
  };
  return <DndContext sensors={sensors} onDragEnd={dragEnd}><div className="kanban-board">{columns.map((column) => <Column key={column.status} {...column} tasks={tasks.filter((x) => x.status === column.status).sort((a, b) => a.position - b.position)} canMove={canMove} onOpen={onOpen} />)}</div></DndContext>;
}
