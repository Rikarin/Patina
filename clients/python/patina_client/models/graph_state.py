from __future__ import annotations

from collections.abc import Mapping
from typing import Any, TypeVar, BinaryIO, TextIO, TYPE_CHECKING, Generator

from attrs import define as _attrs_define
from attrs import field as _attrs_field

from ..types import UNSET, Unset

from ..types import UNSET, Unset
from typing import cast

if TYPE_CHECKING:
  from ..models.graph_comment_state import GraphCommentState
  from ..models.graph_diagnostic import GraphDiagnostic
  from ..models.graph_edge_state import GraphEdgeState
  from ..models.graph_evaluation import GraphEvaluation
  from ..models.graph_group_state import GraphGroupState
  from ..models.graph_history_state import GraphHistoryState
  from ..models.graph_info_state import GraphInfoState
  from ..models.graph_interface_port import GraphInterfacePort
  from ..models.graph_node_state import GraphNodeState
  from ..models.graph_node_time import GraphNodeTime
  from ..models.graph_preset_state import GraphPresetState
  from ..models.graph_preview import GraphPreview
  from ..models.graph_selection import GraphSelection
  from ..models.graph_setting_definition import GraphSettingDefinition
  from ..models.graph_state_settings import GraphStateSettings





T = TypeVar("T", bound="GraphState")



@_attrs_define
class GraphState:
    """ The reply to `GET /api/v1/graphs/{id}`: the whole document as the Server holds it.

        Attributes:
            id (str): The session.
            path (str): The file.
            name (str): The graph's name.
            revision (int): Counts changes to the document, undo included.
            sequence (int): The last event's number: pass it as `since` to follow from exactly here.
            history (GraphHistoryState): What the undo history offers.
            nodes (list[GraphNodeState]): The nodes, by identity.
            edges (list[GraphEdgeState]): The wires, by the input they arrive at.
            settings (GraphStateSettings): Its settings: `baseWidth`, `baseHeight`, `seed` and any others.
            parameters (list[GraphSettingDefinition]): The parameters the graph exposes.
            presets (list[GraphPresetState]): Named sets of values for them.
            interface (list[GraphInterfacePort]): The ports it has when another graph holds it.
            groups (list[GraphGroupState]): Its group frames, in file order.
            comments (list[GraphCommentState]): Its notes, in file order.
            previews (list[GraphPreview]): The newest preview of each node.
            repairs (list[GraphDiagnostic]): What reading the file had to drop or fix.
            settled (int): The newest revision with nothing left to evaluate. Below Revision, evaluation is behind.
            read_only (bool): True when this graph cannot be edited or saved: a compound that ships with Patina, or a
                package's graph.
            info (GraphInfoState): What a graph says about itself to whoever finds it on a shelf or in a package. Nothing it
                says changes what the
                graph makes.
            selection (GraphSelection): The nodes chosen in a graph session, and the one kept on view whatever is chosen. A
                session has one selection,
                whichever client made it last, so a client that follows it, such as the viewport, needs no editor (#244).
            times (list[GraphNodeTime]): The newest GPU time of each node an evaluation has timed.
            evaluation (GraphEvaluation | None | Unset):
     """

    id: str
    path: str
    name: str
    revision: int
    sequence: int
    history: GraphHistoryState
    nodes: list[GraphNodeState]
    edges: list[GraphEdgeState]
    settings: GraphStateSettings
    parameters: list[GraphSettingDefinition]
    presets: list[GraphPresetState]
    interface: list[GraphInterfacePort]
    groups: list[GraphGroupState]
    comments: list[GraphCommentState]
    previews: list[GraphPreview]
    repairs: list[GraphDiagnostic]
    settled: int
    read_only: bool
    info: GraphInfoState
    selection: GraphSelection
    times: list[GraphNodeTime]
    evaluation: GraphEvaluation | None | Unset = UNSET
    additional_properties: dict[str, Any] = _attrs_field(init=False, factory=dict)





    def to_dict(self) -> dict[str, Any]:
        from ..models.graph_comment_state import GraphCommentState # noqa: PLC0415
        from ..models.graph_diagnostic import GraphDiagnostic # noqa: PLC0415
        from ..models.graph_edge_state import GraphEdgeState # noqa: PLC0415
        from ..models.graph_evaluation import GraphEvaluation # noqa: PLC0415
        from ..models.graph_group_state import GraphGroupState # noqa: PLC0415
        from ..models.graph_history_state import GraphHistoryState # noqa: PLC0415
        from ..models.graph_info_state import GraphInfoState # noqa: PLC0415
        from ..models.graph_interface_port import GraphInterfacePort # noqa: PLC0415
        from ..models.graph_node_state import GraphNodeState # noqa: PLC0415
        from ..models.graph_node_time import GraphNodeTime # noqa: PLC0415
        from ..models.graph_preset_state import GraphPresetState # noqa: PLC0415
        from ..models.graph_preview import GraphPreview # noqa: PLC0415
        from ..models.graph_selection import GraphSelection # noqa: PLC0415
        from ..models.graph_setting_definition import GraphSettingDefinition # noqa: PLC0415
        from ..models.graph_state_settings import GraphStateSettings # noqa: PLC0415
        id = self.id

        path = self.path

        name = self.name

        revision = self.revision

        sequence = self.sequence

        history = self.history.to_dict()

        nodes = []
        for nodes_item_data in self.nodes:
            nodes_item = nodes_item_data.to_dict()
            nodes.append(nodes_item)



        edges = []
        for edges_item_data in self.edges:
            edges_item = edges_item_data.to_dict()
            edges.append(edges_item)



        settings = self.settings.to_dict()

        parameters = []
        for parameters_item_data in self.parameters:
            parameters_item = parameters_item_data.to_dict()
            parameters.append(parameters_item)



        presets = []
        for presets_item_data in self.presets:
            presets_item = presets_item_data.to_dict()
            presets.append(presets_item)



        interface = []
        for interface_item_data in self.interface:
            interface_item = interface_item_data.to_dict()
            interface.append(interface_item)



        groups = []
        for groups_item_data in self.groups:
            groups_item = groups_item_data.to_dict()
            groups.append(groups_item)



        comments = []
        for comments_item_data in self.comments:
            comments_item = comments_item_data.to_dict()
            comments.append(comments_item)



        previews = []
        for previews_item_data in self.previews:
            previews_item = previews_item_data.to_dict()
            previews.append(previews_item)



        repairs = []
        for repairs_item_data in self.repairs:
            repairs_item = repairs_item_data.to_dict()
            repairs.append(repairs_item)



        settled = self.settled

        read_only = self.read_only

        info = self.info.to_dict()

        selection = self.selection.to_dict()

        times = []
        for times_item_data in self.times:
            times_item = times_item_data.to_dict()
            times.append(times_item)



        evaluation: dict[str, Any] | None | Unset
        if isinstance(self.evaluation, Unset):
            evaluation = UNSET
        elif isinstance(self.evaluation, GraphEvaluation):
            evaluation = self.evaluation.to_dict()
        else:
            evaluation = self.evaluation


        field_dict: dict[str, Any] = {}
        field_dict.update(self.additional_properties)
        field_dict.update({
            "id": id,
            "path": path,
            "name": name,
            "revision": revision,
            "sequence": sequence,
            "history": history,
            "nodes": nodes,
            "edges": edges,
            "settings": settings,
            "parameters": parameters,
            "presets": presets,
            "interface": interface,
            "groups": groups,
            "comments": comments,
            "previews": previews,
            "repairs": repairs,
            "settled": settled,
            "readOnly": read_only,
            "info": info,
            "selection": selection,
            "times": times,
        })
        if evaluation is not UNSET:
            field_dict["evaluation"] = evaluation

        return field_dict



    @classmethod
    def from_dict(cls: type[T], src_dict: Mapping[str, Any]) -> T:
        from ..models.graph_comment_state import GraphCommentState # noqa: PLC0415
        from ..models.graph_diagnostic import GraphDiagnostic # noqa: PLC0415
        from ..models.graph_edge_state import GraphEdgeState # noqa: PLC0415
        from ..models.graph_evaluation import GraphEvaluation # noqa: PLC0415
        from ..models.graph_group_state import GraphGroupState # noqa: PLC0415
        from ..models.graph_history_state import GraphHistoryState # noqa: PLC0415
        from ..models.graph_info_state import GraphInfoState # noqa: PLC0415
        from ..models.graph_interface_port import GraphInterfacePort # noqa: PLC0415
        from ..models.graph_node_state import GraphNodeState # noqa: PLC0415
        from ..models.graph_node_time import GraphNodeTime # noqa: PLC0415
        from ..models.graph_preset_state import GraphPresetState # noqa: PLC0415
        from ..models.graph_preview import GraphPreview # noqa: PLC0415
        from ..models.graph_selection import GraphSelection # noqa: PLC0415
        from ..models.graph_setting_definition import GraphSettingDefinition # noqa: PLC0415
        from ..models.graph_state_settings import GraphStateSettings # noqa: PLC0415
        d = dict(src_dict)
        id = d.pop("id")

        path = d.pop("path")

        name = d.pop("name")

        revision = d.pop("revision")

        sequence = d.pop("sequence")

        history = GraphHistoryState.from_dict(d.pop("history"))




        nodes = []
        _nodes = d.pop("nodes")
        for nodes_item_data in (_nodes):
            nodes_item = GraphNodeState.from_dict(nodes_item_data)



            nodes.append(nodes_item)


        edges = []
        _edges = d.pop("edges")
        for edges_item_data in (_edges):
            edges_item = GraphEdgeState.from_dict(edges_item_data)



            edges.append(edges_item)


        settings = GraphStateSettings.from_dict(d.pop("settings"))




        parameters = []
        _parameters = d.pop("parameters")
        for parameters_item_data in (_parameters):
            parameters_item = GraphSettingDefinition.from_dict(parameters_item_data)



            parameters.append(parameters_item)


        presets = []
        _presets = d.pop("presets")
        for presets_item_data in (_presets):
            presets_item = GraphPresetState.from_dict(presets_item_data)



            presets.append(presets_item)


        interface = []
        _interface = d.pop("interface")
        for interface_item_data in (_interface):
            interface_item = GraphInterfacePort.from_dict(interface_item_data)



            interface.append(interface_item)


        groups = []
        _groups = d.pop("groups")
        for groups_item_data in (_groups):
            groups_item = GraphGroupState.from_dict(groups_item_data)



            groups.append(groups_item)


        comments = []
        _comments = d.pop("comments")
        for comments_item_data in (_comments):
            comments_item = GraphCommentState.from_dict(comments_item_data)



            comments.append(comments_item)


        previews = []
        _previews = d.pop("previews")
        for previews_item_data in (_previews):
            previews_item = GraphPreview.from_dict(previews_item_data)



            previews.append(previews_item)


        repairs = []
        _repairs = d.pop("repairs")
        for repairs_item_data in (_repairs):
            repairs_item = GraphDiagnostic.from_dict(repairs_item_data)



            repairs.append(repairs_item)


        settled = d.pop("settled")

        read_only = d.pop("readOnly")

        info = GraphInfoState.from_dict(d.pop("info"))




        selection = GraphSelection.from_dict(d.pop("selection"))




        times = []
        _times = d.pop("times")
        for times_item_data in (_times):
            times_item = GraphNodeTime.from_dict(times_item_data)



            times.append(times_item)


        def _parse_evaluation(data: object) -> GraphEvaluation | None | Unset:
            if data is None:
                return data
            if isinstance(data, Unset):
                return data
            try:
                if not isinstance(data, dict):
                    raise TypeError()
                evaluation_type_1 = GraphEvaluation.from_dict(data)



                return evaluation_type_1
            except (TypeError, ValueError, AttributeError, KeyError):
                pass
            return cast(GraphEvaluation | None | Unset, data)

        evaluation = _parse_evaluation(d.pop("evaluation", UNSET))


        graph_state = cls(
            id=id,
            path=path,
            name=name,
            revision=revision,
            sequence=sequence,
            history=history,
            nodes=nodes,
            edges=edges,
            settings=settings,
            parameters=parameters,
            presets=presets,
            interface=interface,
            groups=groups,
            comments=comments,
            previews=previews,
            repairs=repairs,
            settled=settled,
            read_only=read_only,
            info=info,
            selection=selection,
            times=times,
            evaluation=evaluation,
        )


        graph_state.additional_properties = d
        return graph_state

    @property
    def additional_keys(self) -> list[str]:
        return list(self.additional_properties.keys())

    def __getitem__(self, key: str) -> Any:
        return self.additional_properties[key]

    def __setitem__(self, key: str, value: Any) -> None:
        self.additional_properties[key] = value

    def __delitem__(self, key: str) -> None:
        del self.additional_properties[key]

    def __contains__(self, key: str) -> bool:
        return key in self.additional_properties
