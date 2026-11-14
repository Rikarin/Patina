from http import HTTPStatus
from typing import Any, cast
from urllib.parse import quote

import httpx2

from ...client import AuthenticatedClient, Client
from ...types import Response, UNSET
from ... import errors

from ...models.problem_details import ProblemDetails
from ...models.stack_graph_made import StackGraphMade
from ...models.stack_graph_request import StackGraphRequest
from typing import cast



def _get_kwargs(
    id: str,
    *,
    body: StackGraphRequest,

) -> dict[str, Any]:
    headers: dict[str, Any] = {}


    

    

    _kwargs: dict[str, Any] = {
        "method": "post",
        "url": "/api/v1/stacks/{id}/graph".format(id=quote(str(id), safe=""),),
    }

    _kwargs["json"] = body.to_dict()

    headers["Content-Type"] = "application/json"

    _kwargs["headers"] = headers
    return _kwargs



def _parse_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> ProblemDetails | StackGraphMade | None:
    if response.status_code == 200:
        response_200 = StackGraphMade.from_dict(response.json())



        return response_200

    if response.status_code == 404:
        response_404 = ProblemDetails.from_dict(response.json())



        return response_404

    if response.status_code == 409:
        response_409 = ProblemDetails.from_dict(response.json())



        return response_409

    if response.status_code == 422:
        response_422 = ProblemDetails.from_dict(response.json())



        return response_422

    if client.raise_on_unexpected_status:
        raise errors.UnexpectedStatus(response.status_code, response.content)
    else:
        return None


def _build_response(*, client: AuthenticatedClient | Client, response: httpx2.Response) -> Response[ProblemDetails | StackGraphMade]:
    return Response(
        status_code=HTTPStatus(response.status_code),
        content=response.content,
        headers=response.headers,
        parsed=_parse_response(client=client, response=response),
    )


def sync_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: StackGraphRequest,

) -> Response[ProblemDetails | StackGraphMade]:
    """ Writes the graph the stack's shown texture set stands for into its project, as a new graph document
    beside it. The stack is left as it is, and nothing keeps the two in step.

    Args:
        id (str):
        body (StackGraphRequest): Writes the graph a layer stack stands for into its project,
            beside it, as a new graph document (#205). The stack
            is left as it is: the graph is a copy to take further, and nothing keeps the two in step.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | StackGraphMade]
     """


    kwargs = _get_kwargs(
        id=id,
body=body,

    )

    response = client.get_httpx_client().request(
        **kwargs,
    )

    return _build_response(client=client, response=response)

def sync(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: StackGraphRequest,

) -> ProblemDetails | StackGraphMade | None:
    """ Writes the graph the stack's shown texture set stands for into its project, as a new graph document
    beside it. The stack is left as it is, and nothing keeps the two in step.

    Args:
        id (str):
        body (StackGraphRequest): Writes the graph a layer stack stands for into its project,
            beside it, as a new graph document (#205). The stack
            is left as it is: the graph is a copy to take further, and nothing keeps the two in step.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | StackGraphMade
     """


    return sync_detailed(
        id=id,
client=client,
body=body,

    ).parsed

async def asyncio_detailed(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: StackGraphRequest,

) -> Response[ProblemDetails | StackGraphMade]:
    """ Writes the graph the stack's shown texture set stands for into its project, as a new graph document
    beside it. The stack is left as it is, and nothing keeps the two in step.

    Args:
        id (str):
        body (StackGraphRequest): Writes the graph a layer stack stands for into its project,
            beside it, as a new graph document (#205). The stack
            is left as it is: the graph is a copy to take further, and nothing keeps the two in step.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        Response[ProblemDetails | StackGraphMade]
     """


    kwargs = _get_kwargs(
        id=id,
body=body,

    )

    response = await client.get_async_httpx_client().request(
        **kwargs
    )

    return _build_response(client=client, response=response)

async def asyncio(
    id: str,
    *,
    client: AuthenticatedClient | Client,
    body: StackGraphRequest,

) -> ProblemDetails | StackGraphMade | None:
    """ Writes the graph the stack's shown texture set stands for into its project, as a new graph document
    beside it. The stack is left as it is, and nothing keeps the two in step.

    Args:
        id (str):
        body (StackGraphRequest): Writes the graph a layer stack stands for into its project,
            beside it, as a new graph document (#205). The stack
            is left as it is: the graph is a copy to take further, and nothing keeps the two in step.

    Raises:
        errors.UnexpectedStatus: If the server returns an undocumented status code and Client.raise_on_unexpected_status is True.
        httpx2.TimeoutException: If the request takes longer than Client.timeout.

    Returns:
        ProblemDetails | StackGraphMade
     """


    return (await asyncio_detailed(
        id=id,
client=client,
body=body,

    )).parsed
