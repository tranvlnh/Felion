const apiBase = "/api/v1";
const state = {
  references: null,
  candidates: null,
  roleAssignments: null,
  roleCatalog: [],
  selectedCandidate: null,
  selectedTeam: null,
  selectedRoleSubject: null,
  page: 1,
  pageSize: 25,
  activeView: "candidates"
};

const elements = {
  message: document.querySelector("#message"),
  currentUser: document.querySelector("#current-user"),
  candidatesView: document.querySelector("#candidates-view"),
  teamsView: document.querySelector("#teams-view"),
  roleAssignmentsView: document.querySelector("#role-assignments-view"),
  roleAssignmentsTab: document.querySelector("#role-assignments-tab"),
  candidateFilters: document.querySelector("#candidate-filters"),
  candidateTable: document.querySelector("#candidate-table-body"),
  candidateEmpty: document.querySelector("#candidate-empty"),
  pageSummary: document.querySelector("#page-summary"),
  previousPage: document.querySelector("#previous-page-button"),
  nextPage: document.querySelector("#next-page-button"),
  teamList: document.querySelector("#team-list"),
  teamEmpty: document.querySelector("#team-empty"),
  candidateDialog: document.querySelector("#candidate-dialog"),
  candidateForm: document.querySelector("#candidate-form"),
  candidateDetailDialog: document.querySelector("#candidate-detail-dialog"),
  teamDialog: document.querySelector("#team-dialog"),
  teamForm: document.querySelector("#team-form"),
  teamDetailDialog: document.querySelector("#team-detail-dialog"),
  teamChangeDialog: document.querySelector("#team-change-dialog"),
  teamChangeForm: document.querySelector("#team-change-form"),
  roleAssignmentFilters: document.querySelector("#role-assignment-filters"),
  roleAssignmentTable: document.querySelector("#role-assignment-table-body"),
  roleAssignmentEmpty: document.querySelector("#role-assignment-empty"),
  roleAssignmentDialog: document.querySelector("#role-assignment-dialog"),
  roleAssignmentForm: document.querySelector("#role-assignment-form"),
  roleAssignmentSubject: document.querySelector("#role-assignment-subject"),
  roleAssignmentOptions: document.querySelector("#role-assignment-options"),
  confirmDialog: document.querySelector("#confirm-dialog")
};

function escapeHtml(value) {
  return String(value ?? "").replace(/[&<>'"]/g, character => ({
    "&": "&amp;", "<": "&lt;", ">": "&gt;", "'": "&#39;", "\"": "&quot;"
  })[character]);
}

function setMessage(text, kind = "success") {
  elements.message.textContent = text;
  elements.message.className = `message ${kind}`;
  elements.message.hidden = false;
}

function clearMessage() {
  elements.message.hidden = true;
  elements.message.textContent = "";
}

async function api(path, options = {}) {
  const response = await fetch(`${apiBase}${path}`, {
    credentials: "same-origin",
    headers: { "Accept": "application/json", ...(options.body ? { "Content-Type": "application/json" } : {}), ...options.headers },
    ...options
  });
  if (!response.ok) {
    const contentType = response.headers.get("content-type") ?? "";
    const body = contentType.includes("application/json") ? await response.json() : null;
    const detail = body?.detail || body?.title || `Request failed (${response.status}).`;
    const error = new Error(detail);
    error.status = response.status;
    throw error;
  }
  if (response.status === 204) return null;
  return response.json();
}

async function confirmAction(title, message, buttonText = "Xác nhận") {
  document.querySelector("#confirm-title").textContent = title;
  document.querySelector("#confirm-message").textContent = message;
  document.querySelector("#confirm-submit").textContent = buttonText;
  elements.confirmDialog.showModal();
  return new Promise(resolve => {
    elements.confirmDialog.addEventListener("close", () => resolve(elements.confirmDialog.returnValue === "confirm"), { once: true });
  });
}

function statusBadge(status) {
  return `<span class="badge ${String(status).toLowerCase()}">${escapeHtml(status)}</span>`;
}

function selectedValue(selector) {
  return document.querySelector(selector).value;
}

function populateSelect(selector, options, placeholder, currentValue = "") {
  const select = document.querySelector(selector);
  select.replaceChildren();
  const empty = new Option(placeholder, "");
  select.add(empty);
  options.forEach(option => select.add(new Option(option.name, option.id)));
  select.value = currentValue;
}

function populateReferences() {
  const { departments, generations, teams } = state.references;
  populateSelect("#filter-department", departments, "Tất cả");
  populateSelect("#filter-generation", generations, "Tất cả");
  const teamFilter = document.querySelector("#filter-team");
  teamFilter.replaceChildren();
  teamFilter.add(new Option("Tất cả", ""));
  teamFilter.add(new Option("Chưa có team", "unassigned"));
  teams.forEach(team => teamFilter.add(new Option(`${team.name}${team.isActive ? "" : " (inactive)"}`, team.id)));
  populateSelect("#candidate-department", departments, "Chọn Department");
  populateSelect("#candidate-generation", generations, "Chọn Generation");
}

function candidateQuery() {
  const parameters = new URLSearchParams({ page: String(state.page), pageSize: String(state.pageSize) });
  const search = selectedValue("#filter-search").trim();
  const departmentId = selectedValue("#filter-department");
  const generationId = selectedValue("#filter-generation");
  const team = selectedValue("#filter-team");
  const status = selectedValue("#filter-status");
  if (search) parameters.set("search", search);
  if (departmentId) parameters.set("departmentId", departmentId);
  if (generationId) parameters.set("generationId", generationId);
  if (team === "unassigned") parameters.set("hasTeam", "false");
  if (team && team !== "unassigned") parameters.set("teamId", team);
  if (status) parameters.set("status", status);
  return parameters.toString();
}

async function loadCandidates() {
  const page = await api(`/probation/candidates?${candidateQuery()}`);
  state.candidates = page;
  elements.candidateTable.replaceChildren();
  page.items.forEach(candidate => {
    const row = document.createElement("tr");
    row.innerHTML = `<td><div class="candidate-name">${escapeHtml(candidate.fullName)}</div><div class="candidate-student">${escapeHtml(candidate.studentId)}${candidate.hasDiscordIdentity ? " · Discord linked" : ""}</div></td><td>${escapeHtml(candidate.department.name)}</td><td>${escapeHtml(candidate.generation.code || candidate.generation.name)}</td><td>${escapeHtml(candidate.team?.name || "—")}</td><td>${statusBadge(candidate.status)}</td><td><button class="button secondary" type="button" data-candidate-id="${candidate.id}">Chi tiết</button></td>`;
    elements.candidateTable.append(row);
  });
  elements.candidateEmpty.hidden = page.items.length !== 0;
  const totalPages = Math.max(1, Math.ceil(page.totalCount / page.pageSize));
  elements.pageSummary.textContent = `Trang ${page.page}/${totalPages} · ${page.totalCount} candidate`;
  elements.previousPage.disabled = page.page <= 1;
  elements.nextPage.disabled = page.page >= totalPages;
}

async function loadTeams() {
  const teams = await api("/probation/teams");
  state.teams = teams;
  elements.teamList.replaceChildren();
  teams.forEach(team => {
    const card = document.createElement("article");
    card.className = "team-card";
    card.innerHTML = `<div><h3>${escapeHtml(team.name)}</h3><p class="muted">${team.isActive ? "Đang hoạt động" : "Inactive"}</p></div><div class="team-stats"><span>${team.candidateIds.length} candidates</span><span>${team.mentorMemberIds.length} mentors</span></div><button class="button secondary" type="button" data-team-id="${team.id}">Mở team</button>`;
    elements.teamList.append(card);
  });
  elements.teamEmpty.hidden = teams.length !== 0;
}

async function loadRoleAssignments() {
  const dashboard = await api("/discord/role-assignments");
  state.roleAssignments = dashboard.subjects;
  try {
    state.roleCatalog = await api("/discord/role-assignments/roles");
  } catch (error) {
    state.roleCatalog = [];
    renderRoleAssignments();
    throw error;
  }
  renderRoleAssignments();
}

function renderRoleAssignments() {
  const search = selectedValue("#role-assignment-search").trim().toLocaleLowerCase();
  const subjects = (state.roleAssignments || []).filter(subject =>
    !search
    || subject.studentId.toLocaleLowerCase().includes(search)
    || subject.fullName.toLocaleLowerCase().includes(search));
  elements.roleAssignmentTable.replaceChildren();
  subjects.forEach(subject => {
    const type = subject.subjectType === "Member" ? "Member" : "Candidate";
    const statusOrPosition = subject.subjectType === "Member"
      ? subject.memberPosition
      : subject.candidateStatus;
    const assignments = (subject.assignments || [])
      .map(assignment => `<span class="chip compact-chip">${escapeHtml(assignment.roleNameSnapshot)}</span>`)
      .join(" ") || "<span class=\"muted\">Chưa gán</span>";
    const row = document.createElement("tr");
    row.innerHTML = `<td><div class="candidate-name">${escapeHtml(subject.fullName)}</div><div class="candidate-student">${escapeHtml(subject.studentId)}</div></td><td>${type}</td><td>${statusOrPosition ? statusBadge(statusOrPosition) : "—"}</td><td>${subject.discordUserId ? "Đã liên kết" : "Chưa liên kết"}</td><td><div class="chip-list">${assignments}</div></td><td><button class="button secondary" type="button" data-role-subject-type="${escapeHtml(subject.subjectType)}" data-role-subject-id="${escapeHtml(subject.subjectId)}">Chỉnh role</button></td>`;
    elements.roleAssignmentTable.append(row);
  });
  elements.roleAssignmentEmpty.hidden = subjects.length !== 0;
}

function openRoleAssignmentForm(subject) {
  state.selectedRoleSubject = subject;
  elements.roleAssignmentSubject.textContent = `${subject.fullName} · ${subject.studentId} · ${subject.subjectType === "Member" ? "Member" : "Candidate"}`;
  elements.roleAssignmentOptions.replaceChildren();
  const selectedRoleIds = new Set((subject.assignments || []).map(assignment => assignment.discordRoleId));
  state.roleCatalog.forEach(role => {
    const label = document.createElement("label");
    label.className = "role-option";
    label.innerHTML = `<input type="checkbox" value="${escapeHtml(role.id)}"${selectedRoleIds.has(String(role.id)) ? " checked" : ""}><span>${escapeHtml(role.name)}</span><small>Position ${escapeHtml(role.rawPosition)}</small>`;
    elements.roleAssignmentOptions.append(label);
  });
  if (!state.roleCatalog.length) {
    elements.roleAssignmentOptions.textContent = "Guild chưa có role có thể gán.";
  }
  elements.roleAssignmentDialog.showModal();
}

async function openCandidateDetail(candidateId) {
  state.selectedCandidate = await api(`/probation/candidates/${candidateId}`);
  renderCandidateDetail();
  elements.candidateDetailDialog.showModal();
}

function renderCandidateDetail() {
  const candidate = state.selectedCandidate;
  document.querySelector("#candidate-detail-content").innerHTML = `<dl class="detail-list"><dt>MSSV</dt><dd>${escapeHtml(candidate.studentId)}</dd><dt>Họ và tên</dt><dd>${escapeHtml(candidate.fullName)}</dd><dt>Department</dt><dd>${escapeHtml(candidate.department.name)}</dd><dt>Generation</dt><dd>${escapeHtml(candidate.generation.name)}${candidate.generation.code ? ` (${escapeHtml(candidate.generation.code)})` : ""}</dd><dt>Team</dt><dd>${escapeHtml(candidate.team?.name || "Chưa có team")}</dd><dt>Status</dt><dd>${statusBadge(candidate.status)}</dd><dt>Discord</dt><dd>${candidate.hasDiscordIdentity ? "Đã liên kết" : "Chưa liên kết"}</dd></dl>`;
  const active = candidate.status === "Active";
  document.querySelector("#edit-candidate-button").hidden = !active;
  document.querySelector("#change-team-button").hidden = !active;
  document.querySelector("#pass-candidate-button").hidden = !active;
  document.querySelector("#fail-candidate-button").hidden = !active;
}

function openCandidateForm(candidate = null) {
  document.querySelector("#candidate-dialog-title").textContent = candidate ? "Sửa candidate" : "Thêm candidate";
  document.querySelector("#candidate-id").value = candidate?.id ?? "";
  document.querySelector("#candidate-student-id").value = candidate?.studentId ?? "";
  document.querySelector("#candidate-full-name").value = candidate?.fullName ?? "";
  document.querySelector("#candidate-department").value = candidate?.department.id ?? "";
  document.querySelector("#candidate-generation").value = candidate?.generation.id ?? "";
  elements.candidateDialog.showModal();
}

function openTeamForm(team = null) {
  document.querySelector("#team-dialog-title").textContent = team ? "Sửa team" : "Tạo team";
  document.querySelector("#team-id").value = team?.id ?? "";
  document.querySelector("#team-name").value = team?.name ?? "";
  document.querySelector("#team-active").checked = team?.isActive ?? true;
  elements.teamDialog.showModal();
}

async function openTeamDetail(teamId) {
  state.selectedTeam = await api(`/probation/teams/${teamId}`);
  renderTeamDetail();
  elements.teamDetailDialog.showModal();
}

function renderTeamDetail() {
  const team = state.selectedTeam;
  document.querySelector("#team-detail-title").textContent = `${team.name}${team.isActive ? "" : " (inactive)"}`;
  const mentors = document.querySelector("#team-mentor-list");
  mentors.replaceChildren();
  (team.mentors || []).forEach(mentor => {
    const chip = document.createElement("span");
    chip.className = "chip";
    chip.innerHTML = `${escapeHtml(mentor.fullName)} · ${escapeHtml(mentor.studentId)}${team.isActive ? `<button type="button" aria-label="Remove ${escapeHtml(mentor.fullName)}" data-remove-mentor="${mentor.memberId}">×</button>` : ""}`;
    mentors.append(chip);
  });
  if (!team.mentors?.length) mentors.textContent = "Chưa có mentor.";
  const candidates = document.querySelector("#team-candidate-list");
  candidates.replaceChildren();
  (team.candidates || []).forEach(candidate => {
    const chip = document.createElement("span");
    chip.className = "chip";
    chip.textContent = `${candidate.fullName} · ${candidate.studentId}`;
    candidates.append(chip);
  });
  if (!team.candidates?.length) candidates.textContent = "Chưa có candidate.";
  document.querySelector("#mentor-search-form").hidden = !team.isActive;
}

function openTeamChange() {
  const select = document.querySelector("#team-change-select");
  select.replaceChildren();
  select.add(new Option("Chưa có team", ""));
  state.references.teams.filter(team => team.isActive).forEach(team => select.add(new Option(team.name, team.id)));
  select.value = state.selectedCandidate.team?.id ?? "";
  elements.teamChangeDialog.showModal();
}

async function makeDecision(decision) {
  const candidate = state.selectedCandidate;
  const confirmed = await confirmAction(
    `${decision.toUpperCase()} candidate`,
    decision === "Pass"
      ? `PASS ${candidate.fullName}? Thao tác sẽ tạo Member, chuyển Discord identity và không thể hoàn tác.`
      : `FAIL ${candidate.fullName}? Thao tác sẽ kick Discord user nếu đã liên kết và không thể hoàn tác.`,
    decision === "Pass" ? "PASS candidate" : "FAIL candidate");
  if (!confirmed) return;
  const results = await api("/probation/decisions", { method: "POST", body: JSON.stringify({ items: [{ candidateId: candidate.id, decision }] }) });
  const result = results[0];
  if (!result.succeeded) throw new Error(result.error || "Không thể quyết định candidate.");
  elements.candidateDetailDialog.close();
  setMessage(`${decision.toUpperCase()} thành công cho ${candidate.fullName}.`);
  await Promise.all([loadCandidates(), loadTeams()]);
}

async function initialize() {
  try {
    const currentUser = await api("/auth/me");
    if (!["Admin", "Core"].includes(currentUser.position)) {
      setMessage("Tài khoản hiện tại không có quyền Core/Admin.", "error");
      return;
    }
    elements.currentUser.textContent = `${currentUser.fullName} · ${currentUser.position}`;
    const isAdmin = currentUser.position === "Admin";
    elements.roleAssignmentsTab.hidden = !isAdmin;
    state.references = await api("/probation/candidates/reference-data");
    populateReferences();
    await Promise.all([loadCandidates(), loadTeams()]);
    if (isAdmin) {
      try {
        await loadRoleAssignments();
      } catch (error) {
        setMessage(`Không tải được danh sách Discord role: ${error.message}`, "error");
      }
    }
  } catch (error) {
    if (error.status === 401) {
      const returnUrl = encodeURIComponent("/admin/probation/");
      window.location.assign(`${apiBase}/auth/login?returnUrl=${returnUrl}`);
      return;
    }
    setMessage(error.message, "error");
  }
}

document.querySelectorAll("[data-view]").forEach(tab => tab.addEventListener("click", () => {
  state.activeView = tab.dataset.view;
  document.querySelectorAll("[data-view]").forEach(item => item.classList.toggle("active", item === tab));
  elements.candidatesView.hidden = state.activeView !== "candidates";
  elements.teamsView.hidden = state.activeView !== "teams";
  elements.roleAssignmentsView.hidden = state.activeView !== "role-assignments";
}));

document.querySelector("#refresh-button").addEventListener("click", async () => {
  clearMessage();
  try {
    await Promise.all([
      loadCandidates(),
      loadTeams(),
      ...(elements.roleAssignmentsTab.hidden ? [] : [loadRoleAssignments()])
    ]);
  } catch (error) { setMessage(error.message, "error"); }
});
document.querySelector("#add-candidate-button").addEventListener("click", () => openCandidateForm());
document.querySelector("#add-team-button").addEventListener("click", () => openTeamForm());
elements.candidateFilters.addEventListener("submit", async event => { event.preventDefault(); state.page = 1; try { await loadCandidates(); } catch (error) { setMessage(error.message, "error"); } });
document.querySelector("#clear-filters-button").addEventListener("click", async () => { elements.candidateFilters.reset(); state.page = 1; await loadCandidates(); });
elements.roleAssignmentFilters.addEventListener("submit", event => { event.preventDefault(); renderRoleAssignments(); });
document.querySelector("#clear-role-assignment-filter").addEventListener("click", () => { elements.roleAssignmentFilters.reset(); renderRoleAssignments(); });
elements.previousPage.addEventListener("click", async () => { state.page -= 1; await loadCandidates(); });
elements.nextPage.addEventListener("click", async () => { state.page += 1; await loadCandidates(); });
elements.candidateTable.addEventListener("click", event => { const button = event.target.closest("[data-candidate-id]"); if (button) openCandidateDetail(button.dataset.candidateId).catch(error => setMessage(error.message, "error")); });
elements.teamList.addEventListener("click", event => { const button = event.target.closest("[data-team-id]"); if (button) openTeamDetail(button.dataset.teamId).catch(error => setMessage(error.message, "error")); });
elements.roleAssignmentTable.addEventListener("click", event => {
  const button = event.target.closest("[data-role-subject-id]");
  if (!button) return;
  const subject = state.roleAssignments.find(item =>
    item.subjectType === button.dataset.roleSubjectType && item.subjectId === button.dataset.roleSubjectId);
  if (subject) openRoleAssignmentForm(subject);
});
document.querySelectorAll("[data-close]").forEach(button => button.addEventListener("click", () => document.querySelector(`#${button.dataset.close}`).close()));

elements.candidateForm.addEventListener("submit", async event => {
  event.preventDefault();
  try {
    const id = selectedValue("#candidate-id");
    const payload = { studentId: selectedValue("#candidate-student-id"), fullName: selectedValue("#candidate-full-name"), departmentId: selectedValue("#candidate-department"), generationId: selectedValue("#candidate-generation") };
    await api(id ? `/probation/candidates/${id}` : "/probation/candidates", { method: id ? "PATCH" : "POST", body: JSON.stringify(payload) });
    elements.candidateDialog.close();
    setMessage(id ? "Đã cập nhật candidate." : "Đã thêm candidate.");
    await loadCandidates();
  } catch (error) { setMessage(error.message, "error"); }
});

document.querySelector("#edit-candidate-button").addEventListener("click", () => openCandidateForm(state.selectedCandidate));
document.querySelector("#change-team-button").addEventListener("click", openTeamChange);
elements.teamChangeForm.addEventListener("submit", async event => {
  event.preventDefault();
  const teamId = selectedValue("#team-change-select") || null;
  try {
    if (state.selectedCandidate.team && teamId !== state.selectedCandidate.team.id) {
      const message = teamId === null
        ? `Gỡ ${state.selectedCandidate.fullName} khỏi team hiện tại?`
        : `Chuyển ${state.selectedCandidate.fullName} sang team mới?`;
      const confirmed = await confirmAction("Thay đổi team candidate", message);
      if (!confirmed) return;
    }
    state.selectedCandidate = await api(`/probation/candidates/${state.selectedCandidate.id}/team`, { method: "PUT", body: JSON.stringify({ teamId }) });
    elements.teamChangeDialog.close();
    renderCandidateDetail();
    setMessage("Đã cập nhật team candidate.");
    await Promise.all([loadCandidates(), loadTeams()]);
  } catch (error) { setMessage(error.message, "error"); }
});
document.querySelector("#pass-candidate-button").addEventListener("click", () => makeDecision("Pass").catch(error => setMessage(error.message, "error")));
document.querySelector("#fail-candidate-button").addEventListener("click", () => makeDecision("Fail").catch(error => setMessage(error.message, "error")));

elements.teamForm.addEventListener("submit", async event => {
  event.preventDefault();
  const id = selectedValue("#team-id");
  const payload = { name: selectedValue("#team-name"), isActive: document.querySelector("#team-active").checked };
  try {
    if (id && !payload.isActive) {
      const confirmed = await confirmAction("Deactivate team", `Deactivate ${payload.name}? Candidate mới sẽ không thể được gán vào team này.`);
      if (!confirmed) return;
    }
    await api(id ? `/probation/teams/${id}` : "/probation/teams", { method: id ? "PATCH" : "POST", body: JSON.stringify(payload) });
    elements.teamDialog.close();
    state.references = await api("/probation/candidates/reference-data");
    populateReferences();
    await Promise.all([loadCandidates(), loadTeams()]);
    setMessage(id ? "Đã cập nhật team." : "Đã tạo team.");
  } catch (error) { setMessage(error.message, "error"); }
});

document.querySelector("#edit-team-button").addEventListener("click", () => openTeamForm(state.selectedTeam));
elements.roleAssignmentForm.addEventListener("submit", async event => {
  event.preventDefault();
  const subject = state.selectedRoleSubject;
  if (!subject) return;
  const discordRoleIds = [...elements.roleAssignmentOptions.querySelectorAll("input[type=checkbox]:checked")]
    .map(input => input.value);
  try {
    const updated = await api(`/discord/role-assignments/${subject.subjectType}/${subject.subjectId}`, {
      method: "PUT",
      body: JSON.stringify({ discordRoleIds })
    });
    const index = state.roleAssignments.findIndex(item =>
      item.subjectType === updated.subjectType && item.subjectId === updated.subjectId);
    if (index >= 0) state.roleAssignments[index] = updated;
    elements.roleAssignmentDialog.close();
    renderRoleAssignments();
    setMessage("Đã cập nhật Discord role riêng. Nếu đã link, hệ thống đã xếp hàng sync role.");
  } catch (error) { setMessage(error.message, "error"); }
});
document.querySelector("#mentor-search-form").addEventListener("submit", async event => {
  event.preventDefault();
  try {
    const mentors = await api(`/probation/candidates/mentor-options?${new URLSearchParams({ search: selectedValue("#mentor-search"), limit: "20" })}`);
    const results = document.querySelector("#mentor-search-results");
    results.replaceChildren();
    mentors.filter(mentor => !state.selectedTeam.mentorMemberIds.includes(mentor.memberId)).forEach(mentor => {
      const row = document.createElement("div");
      row.className = "search-result";
      row.innerHTML = `<span>${escapeHtml(mentor.fullName)} · ${escapeHtml(mentor.studentId)}</span><button class="button secondary" type="button" data-add-mentor="${mentor.memberId}">Thêm</button>`;
      results.append(row);
    });
    if (!results.childElementCount) results.textContent = "Không tìm thấy mentor phù hợp.";
  } catch (error) { setMessage(error.message, "error"); }
});
document.querySelector("#mentor-search-results").addEventListener("click", async event => {
  const button = event.target.closest("[data-add-mentor]");
  if (!button) return;
  try {
    state.selectedTeam = await api(`/probation/teams/${state.selectedTeam.id}/mentors/${button.dataset.addMentor}`, { method: "PUT" });
    document.querySelector("#mentor-search-results").replaceChildren();
    renderTeamDetail();
    await loadTeams();
    setMessage("Đã thêm mentor.");
  } catch (error) { setMessage(error.message, "error"); }
});
document.querySelector("#team-mentor-list").addEventListener("click", async event => {
  const button = event.target.closest("[data-remove-mentor]");
  if (!button) return;
  const mentor = state.selectedTeam.mentors.find(item => item.memberId === button.dataset.removeMentor);
  if (!await confirmAction("Gỡ mentor", `Gỡ ${mentor?.fullName || "mentor"} khỏi ${state.selectedTeam.name}?`)) return;
  try {
    state.selectedTeam = await api(`/probation/teams/${state.selectedTeam.id}/mentors/${button.dataset.removeMentor}`, { method: "DELETE" });
    renderTeamDetail();
    await loadTeams();
    setMessage("Đã gỡ mentor.");
  } catch (error) { setMessage(error.message, "error"); }
});

initialize();
